#pragma warning disable SA1027 // Use tabs correctly

namespace Our.Umbraco.FileSystemProviders.ObjectStorage {
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using global::Umbraco.Core.IO;
    using OpenStack.ObjectStorage.v1;
    using OpenStack.ObjectStorage.v1.ContentObjectFilters;
    using OpenStack.ObjectStorage.v1.Metadata.ContainerMetadata;
    using OpenStack.ObjectStorage.v1.Metadata.ContainerObjectMetadata;
    using OpenStack.ObjectStorage.v1.Serialization;

    /// <summary>
    /// Manage the OpenStack ObjectStorage service
    /// </summary>
    internal class ObjectStorageServiceDriver : IFileSystem
    {
        private static List<ObjectStorageServiceDriver> serviceDriverInstances = new List<ObjectStorageServiceDriver>();

        /// <summary>
        /// The delimiter.
        /// </summary>
        public const string Delimiter = FileSystemPathHelper.Delimiter;

        /// <summary>
        /// Our object to lock against during initialization.
        /// </summary>
        private static readonly object Locker = new object();

        /// <summary>
        /// The key of instance.
        /// </summary>
        private readonly string instanceKey;

        /// <summary>
        /// The root path of project for internal access.
        /// </summary>
        private readonly string rootProjectUrl;

        /// <summary>
        /// The root path of container for internal access.
        /// </summary>
        private readonly string rootContainerUrl;

        /// <summary>
        /// The root url of container for public access.
        /// </summary>
        private readonly string rootHostUrl;

        /// <summary>
        /// The OpenStack ObjectStorage service.
        /// </summary>
        private readonly ObjectStorageService objectStorageService;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectStorageServiceDriver"/> class.
        /// </summary>
        /// <param name="containerName">The container name.</param>
        /// <param name="rootUrl">The root url.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="maxDays">The maximum number of days to cache blob items for in the browser.</param>
        /// <param name="useDefaultRoute">Whether to use the default "media" route in the url independent of the blob container.</param>
        /// <param name="usePrivateContainer">blob container can be private (no direct access) or public (direct access possible, default)</param>
        protected ObjectStorageServiceDriver(string containerName, string rootUrl, string connectionString, int maxDays, bool useDefaultRoute, bool usePrivateContainer)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            this.DisableVirtualPathProvider = this.GetConfigValueBool(Constants.Configuration.DisableVirtualPathProviderKey, false);

            var useEmulator = this.GetConfigValueBool(Constants.Configuration.UseStorageEmulatorKey, false);

            var connectionStringParser = new ConnectionStringParser();
            var connectionStringData = connectionStringParser.Decode(connectionString);

            var authentication = new net.openstack.Core.Providers.OpenStackIdentityProvider(
                new System.Uri(connectionStringData.UrlBase),
                new net.openstack.Core.Domain.CloudIdentityWithProject()
                {
                    Username = connectionStringData.Username,
                    Password = connectionStringData.Password,
                    ProjectId = new net.openstack.Core.Domain.ProjectId(connectionStringData.ProjectId),
                    ProjectName = string.Empty,
                });

            this.objectStorageService = new ObjectStorageService(authentication, connectionStringData.Region, false);

            this.instanceKey = CreateInstanceKey(containerName, rootUrl, connectionString, useDefaultRoute);
            this.ProjectId = connectionStringData.ProjectId;
            this.ContainerName = containerName;
            this.Region = connectionStringData.Region;
            this.MaxDays = maxDays;
            this.UseDefaultRoute = useDefaultRoute;
            this.UsePrivateContainer = usePrivateContainer;

            var rootContainerUrlTask = Task.Run(() => this.objectStorageService.GetContainerUrlAsync(this.ContainerName));
            if (rootContainerUrlTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to connect to server.");
            }

            this.rootContainerUrl = rootContainerUrlTask.Result;
            if (this.rootContainerUrl.EndsWith("/") == false)
            {
                this.rootContainerUrl += "/";
            }

            this.rootProjectUrl = this.rootContainerUrl.Substring(0, this.rootContainerUrl.Length - (this.ContainerName.Length + 1));

            this.rootHostUrl = rootUrl;
            if (string.IsNullOrEmpty(this.rootHostUrl))
            {
                this.rootHostUrl = null;
            }
            else if (this.rootHostUrl.EndsWith("/") == false)
            {
                this.rootHostUrl += "/";
            }

            this.LogHelper = new WrappedLogHelper();
            this.MimeTypeResolver = new MimeTypeResolver();

            this.InitializeContainer();
        }

        /// <summary>
        /// Gets or sets the log helper.
        /// </summary>
        public ILogHelper LogHelper { get; set; }

        /// <summary>
        /// Gets or sets the MIME type resolver.
        /// </summary>
        public IMimeTypeResolver MimeTypeResolver { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to disable the virtual path provider.
        /// </summary>
        public bool DisableVirtualPathProvider { get; set; }

        /// <summary>
        /// Blob container can be private (no direct access) or public (direct access possible, default)
        /// </summary>
        public bool UsePrivateContainer { get; }

        /// <summary>
        /// Gets the project id.
        /// </summary>
        public string ProjectId { get; }

        /// <summary>
        /// Gets the container name.
        /// </summary>
        public string ContainerName { get; }

        /// <summary>
        /// Gets the region.
        /// </summary>
        public string Region { get; }

        /// <summary>
        /// Gets the maximum number of days to cache blob items for in the browser.
        /// </summary>
        public int MaxDays { get; }

        /// <summary>
        /// Gets a value indicating whether to use the default "media" route in the url
        /// independent of the blob container.
        /// </summary>
        public bool UseDefaultRoute { get; }

        /// <summary>
        /// Returns a singleton instance of the <see cref="ObjectStorageServiceDriver"/> class.
        /// </summary>
        /// <param name="containerName">The container name.</param>
        /// <param name="rootUrl">The root url.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="maxDays">The maximum number of days to cache blob items for in the browser.</param>
        /// <param name="useDefaultRoute">Whether to use the default "media" route in the url independent of the blob container.</param>
        /// <param name="usePrivateContainer">blob container can be private (no direct access) or public (direct access possible, default)</param>
        /// <returns>The <see cref="ObjectStorageServiceDriver"/></returns>
        public static ObjectStorageServiceDriver GetInstance(string containerName, string rootUrl, string connectionString, int maxDays, bool useDefaultRoute, bool usePrivateContainer)
        {
            var newestInstanceKey = CreateInstanceKey(containerName, rootUrl, connectionString, useDefaultRoute);

            lock (Locker)
            {
                var fileSystem = serviceDriverInstances.SingleOrDefault(fs => fs.instanceKey == newestInstanceKey);

                if (fileSystem == null)
                {
                    if (maxDays < 0)
                    {
                        maxDays = Constants.DefaultMaxDays;
                    }

                    fileSystem = new ObjectStorageServiceDriver(containerName, rootUrl, connectionString, maxDays, useDefaultRoute, usePrivateContainer);

                    serviceDriverInstances.Add(fileSystem);
                }

                return fileSystem;
            }
        }
        
        /// <summary>
        /// Gets all directories matching the given path.
        /// </summary>
        /// <param name="path">The path to the directories.</param>
        /// <returns>
        /// The <see cref="IEnumerable{String}"/> representing the matched directories.
        /// </returns>
        public IEnumerable<string> GetDirectories(string path)
        {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var searchPrefixFilterValue = FileSystemPathHelper.Instance.PathWithoutDelimiter(fixedPath);
            if (searchPrefixFilterValue != string.Empty)
            {
                searchPrefixFilterValue += "/";
			}

            var filterCollection = new ContentObjectFilterCollection(new IContentObjectFilter[]
            {
                new SearchPrefixContentObjectFilter() { PathPrefix = searchPrefixFilterValue },
                new DelimiterContentObjectFilter(),
            });
            var responseTask = Task.Run(() => this.objectStorageService.GetContainerContentAsync(containerName, filterCollection));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            var folderContent = responseTask.Result;

            return folderContent
                .OfType<ContainerDirectory>()
                .Select(item => FileSystemPathHelper.Instance.PathWithoutDelimiter(item.FullName));
        }

        /// <summary>
        /// Deletes the specified directory.
        /// </summary>
        /// <param name="path">The name of the directory to remove.</param>
        public void DeleteDirectory(string path)
        {
            this.DeleteDirectory(path, false);
        }

        /// <summary>
        /// Deletes the specified directory and, if indicated, any subdirectories and files in the directory.
        /// </summary>
        /// <remarks>ObjectStorage storage has no real concept of directories so deletion is always recursive.</remarks>
        /// <param name="path">The name of the directory to remove.</param>
        /// <param name="recursive">
        /// <c>true</c> to remove directories, subdirectories, and files in path; otherwise, <c>false</c>.
        /// </param>
        public void DeleteDirectory(string path, bool recursive)
        {
            // Note
            // ObjectStorage does not has very concept of folder. Folders are simply a part of Object name.
            // Where there are not objects with "folder" there are not folders.

            if (recursive)
            {
                string containerName;
                var fixedPath = this.ParsePath(path, out containerName);

                var filterCollection = new ContentObjectFilterCollection(new IContentObjectFilter[]
                {
                    new SearchPrefixContentObjectFilter() { PathPrefix = FileSystemPathHelper.Instance.PathWithDelimiter(fixedPath) },
                });
                var contentTask = Task.Run(() => this.objectStorageService.GetContainerContentAsync(containerName, filterCollection));
                if (contentTask.Wait(Constants.WaitTaskTimeout) == false)
                {
                    throw new TimeoutException("Unable to send command to server.");
                }

                var files = contentTask.Result.OfType<ContainerObject>();
                var deleteResultTask = Task.Run(() => this.objectStorageService.DeleteContainerObjectListAsync(containerName, files.Select(item => item.FullName)));
                if (deleteResultTask.Wait(Constants.WaitTaskTimeout) == false)
                {
                    throw new TimeoutException("Unable to send command to server.");
                }
            }
            else
            {
                if (this.DirectoryExists(path))
                {
                    throw new InvalidOperationException($"The directory at path '{path}' is not empty.");
                }
            }
        }

        /// <summary>
        /// Determines whether the specified directory exists.
        /// </summary>
        /// <param name="path">The directory to check.</param>
        /// <returns>
        /// <c>True</c> if the directory exists and the user has permission to view it; otherwise <c>false</c>.
        /// </returns>
        public bool DirectoryExists(string path)
        {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var filterCollection = new ContentObjectFilterCollection(new IContentObjectFilter[]
            {
                new SearchPrefixContentObjectFilter() { PathPrefix = FileSystemPathHelper.Instance.PathWithDelimiter(fixedPath) },
                new TakeContentObjectFilter() { TakeLimit = 1 },
            });
            var responseTask = Task.Run(() => this.objectStorageService.GetContainerContentAsync(containerName, filterCollection));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            return responseTask.Result.Count > 0;
        }

        /// <summary>
        /// Adds a file to the file system.
        /// </summary>
        /// <param name="path">The path to the given file.</param>
        /// <param name="stream">The <see cref="Stream"/> containing the file contents.</param>
        public void AddFile(string path, Stream stream)
        {
            this.AddFile(path, stream, true);
        }

        /// <summary>
        /// Adds a file to the file system.
        /// </summary>
        /// <param name="path">The path to the given file.</param>
        /// <param name="stream">The <see cref="Stream"/> containing the file contents.</param>
        /// <param name="overrideIfExists">Whether to override the file if it already exists.</param>
        public void AddFile(string path, Stream stream, bool overrideIfExists) {
            if (overrideIfExists == false)
            {
                var fileExists = this.FileExists(path);
                if (fileExists)
                {
                    throw new InvalidOperationException($"A file at path '{path}' already exists");
                }
            }

            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

	        stream.Position = 0;
	        using (var streamWrapper = new ReadSeekableStream(stream, 0))
	        {
				var responseTask = Task.Run(() => this.objectStorageService.UpdateContainerObjectAsync(containerName, fixedPath, streamWrapper));
				if (responseTask.Wait(Constants.WaitStreamTaskTimeout) == false)
				{
					throw new TimeoutException("Unable to send command to server.");
				}
	        }
        }

        /// <summary>
        /// Gets all files matching the given path.
        /// </summary>
        /// <param name="path">The path to the files.</param>
        /// <returns>
        /// The <see cref="IEnumerable{String}"/> representing the matched files.
        /// </returns>
        public IEnumerable<string> GetFiles(string path)
        {
            return this.GetFiles(path, "*.*");
        }

        /// <summary>
        /// Gets all files matching the given path and filter.
        /// </summary>
        /// <param name="path">The path to the files.</param>
        /// <param name="filter">A filter that allows the querying of file extension. <example>*.jpg</example></param>
        /// <returns>
        /// The <see cref="IEnumerable{String}"/> representing the matched files.
        /// </returns>
        public IEnumerable<string> GetFiles(string path, string filter)
        {
            string startsWithFilter = string.Empty;
            string endsWithFilter = string.Empty;

            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            if (string.IsNullOrEmpty(filter) == false && filter != "*" && filter != "*.*")
            {
                if (filter.StartsWith("*"))
                {
                    endsWithFilter = filter.TrimStart('*');
                }
                else if (filter.EndsWith("*"))
                {
                    startsWithFilter = filter.TrimEnd('*');
                }
                else if (filter.IndexOf("*", StringComparison.InvariantCulture) > 0)
                {
                    var pos = filter.IndexOf("*", StringComparison.InvariantCulture);
                    startsWithFilter = filter.Substring(0, pos);
                    endsWithFilter = filter.Substring(pos).TrimStart('*');
                }
                else
                {
                    startsWithFilter = filter;
                }
            }

            var filterCollection = new ContentObjectFilterCollection(new IContentObjectFilter[]
            {
                new PathContentObjectFilter() { Path = FileSystemPathHelper.Instance.PathWithoutDelimiter(fixedPath) },
            });
            var responseTask = Task.Run(() => this.objectStorageService.GetContainerContentAsync(containerName, filterCollection));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            var files = responseTask.Result
                .OfType<ContainerObject>()
                .Where(item =>
                {
                    var fileName = FileSystemPathHelper.Instance.FileNameWithExt(item.FullName);
                    if (startsWithFilter != string.Empty && fileName.StartsWith(startsWithFilter) == false)
                    {
                        return false;
                    }

                    if (endsWithFilter != string.Empty && fileName.EndsWith(endsWithFilter) == false)
                    {
                        return false;
                    }

                    return true;
                })
                .Select(item =>
	            {
		            return this.GetRelativePath(item.FullName);
	            });

            return files;
        }

        /// <summary>
        /// Gets a <see cref="Stream"/> containing the contains of the given file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>
        /// <see cref="Stream"/>.
        /// </returns>
        public Stream OpenFile(string path)
        {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var responseTask = Task.Run(() => this.objectStorageService.GetContainerObjectAsync(containerName, fixedPath));
            if (responseTask.Wait(Constants.WaitStreamTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            return responseTask.Result;
        }
        
        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The name of the file to remove.</param>
        public void DeleteFile(string path)
        {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var responseTask = Task.Run(() => this.objectStorageService.DeleteContainerObjectAsync(containerName, fixedPath));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }
        }
        
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns>
        /// <c>True</c> if the file exists and the user has permission to view it; otherwise <c>false</c>.
        /// </returns>
        public bool FileExists(string path)
        {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var responseTask = Task.Run(() => this.objectStorageService.CheckContainerObjectExistsAsync(containerName, fixedPath));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            return responseTask.Result;
        }

        /// <summary>
        /// Gets the relative path.
        /// </summary>
        /// <param name="fullPathOrUrl">The full path or url.</param>
        /// <returns>The path, relative to this filesystem's root.</returns>
        public string GetRelativePath(string fullPathOrUrl) {
            return this.ResolveUrl(fullPathOrUrl, true);
        }

        /// <summary>
        /// Gets the full path.
        /// </summary>
        /// <param name="path">The full or relative path.</param>
        /// <returns>The full path.</returns>
        public string GetFullPath(string path) {
            return this.ResolveUrl(path, false);
        }

        /// <summary>
        /// Returns the url to the media item.
        /// </summary>
        /// <remarks>If the virtual path provider is enabled this returns a relative url.</remarks>
        /// <param name="path">The path to return the url for.</param>
        /// <returns>
        /// <see cref="string"/>.
        /// </returns>
        public string GetUrl(string path) {
            if (this.DisableVirtualPathProvider)
            {
                return this.ResolveUrl(path, false);
            }

            return this.ResolveUrl(path, true);
        }

        /// <summary>
        /// Gets the last modified date/time of the file, expressed as a UTC value.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>
        /// <see cref="DateTimeOffset"/>.
        /// </returns>
        public DateTimeOffset GetLastModified(string path) {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var responseTask = Task.Run(() => this.objectStorageService.ReadContainerObjectMetadataAsync(containerName, fixedPath));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            var metadata = responseTask.Result;
            var timestampMetadata = metadata
                .OfType<LastModifiedContainerObjectMetadata>()
                .FirstOrDefault();

            // Notice: if file does not exists the metadata does not contains "LastModified" data.
            if (timestampMetadata == null)
            {
                return default(DateTimeOffset);
            }
            else
            {
                return timestampMetadata.LastModified;
            }
        }

        /// <summary>
        /// Gets the created date/time of the file, expressed as a UTC value.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>
        /// <see cref="DateTimeOffset"/>.
        /// </returns>
        public DateTimeOffset GetCreated(string path)
        {
            string containerName;
            var fixedPath = this.ParsePath(path, out containerName);

            var responseTask = Task.Run(() => this.objectStorageService.ReadContainerObjectMetadataAsync(containerName, fixedPath));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }

            var metadata = responseTask.Result;
            var timestampMetadata = metadata
                .OfType<TimestampContainerObjectMetadata>()
                .FirstOrDefault();

            // Notice: if file does not exists the metadata does not contains "Timestamp" data.
            if (timestampMetadata == null)
            {
                return default(DateTimeOffset);
            }
            else
            {
                return timestampMetadata.TimestampDate;
            }
        }

        /// <summary>
        /// Returns the instance Key for constructor arguments.
        /// </summary>
        /// <param name="containerName">The container name.</param>
        /// <param name="rootUrl">The root url.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="useDefaultRoute">Whether to use the default "media" route in the url independent of the blob container.</param>
        /// <returns>The <see cref="ObjectStorageServiceDriver"/> instance key</returns>
        protected static string CreateInstanceKey(string containerName, string rootUrl, string connectionString, bool useDefaultRoute)
        {
            return $"{connectionString}/{rootUrl}/{containerName}";
        }

        /// <summary>
        /// Returns the correct url to the media item.
        /// </summary>
        /// <param name="path">The path to the item to return.</param>
        /// <param name="relative">Whether to return a relative path.</param>
        /// <returns>
        /// <see cref="string"/>.
        /// </returns>
        protected string ResolveUrl(string path, bool relative)
        {
            // First create the full url
            string fixedPath = path.Replace(@"\", "/");

            if (!relative)
            {
                // Absolute path
                if (System.Uri.IsWellFormedUriString(fixedPath, UriKind.Absolute))
                {
                    // Is already an absolute path
                    return fixedPath;
                }
                else if (string.IsNullOrEmpty(fixedPath) || fixedPath == "/")
                {
                    // Requested path is empty. Return root path
                    return this.rootHostUrl ?? this.rootContainerUrl;
                }
                else
                {
                    // Join root path with requested path

                    string containerName;
                    fixedPath = this.ParsePath(fixedPath, out containerName);

                    var isContainerPath = (containerName == this.ContainerName) || (this.UseDefaultRoute && containerName == Constants.DefaultMediaRoute);
                    if (isContainerPath && this.rootHostUrl != null)
                    {
                        return $"{this.rootHostUrl}{fixedPath}";
                    }
                    else
                    {
                        return $"{this.rootProjectUrl}{containerName}/{fixedPath}";
                    }
                }
            }
            else
            {
                // Relative path

                var containerName = this.UseDefaultRoute ? Constants.DefaultMediaRoute : this.ContainerName;

                if (System.Uri.IsWellFormedUriString(fixedPath, UriKind.Absolute))
                {
                    // Path is absolute
                    if (this.rootHostUrl != null && fixedPath.StartsWith(this.rootHostUrl))
                    {
                        return "/" + containerName + fixedPath.Substring(this.rootHostUrl.Length - 1);	// Why "- 1"? Because I need to select "/"
                    }
                    else if (fixedPath.StartsWith(this.rootProjectUrl))
                    {
                        return fixedPath.Substring(this.rootProjectUrl.Length - 1); // Why "- 1"? Because I need to select "/"
                    }
                    else
                    {
                        // Invalid URL: it is not managed by current FileSystem.
                        return null;
                    }
                }
                else if (fixedPath == string.Empty || fixedPath == "/")
                {
                    return $"/{containerName}";
                }
                else if (fixedPath.StartsWith("/"))
                {
                    return fixedPath;
                }
                else
                {
                    return $"/{containerName}/{fixedPath}";
                }
            }
        }

        /// <summary>
        /// Parse "path" then split container's path by containerName.
        /// </summary>
        /// <param name="path">The path to parse</param>
        /// <param name="containerName">ContainerName found in path, or <see cref="ObjectStorageServiceDriver.ContainerName"/> when not found.</param>
        /// <returns>Returns the path without container component.</returns>
        protected string ParsePath(string path, out string containerName)
        {
            var fixedPath = path.Replace(@"\", "/");

	        if (this.rootHostUrl != null && fixedPath.StartsWith(this.rootHostUrl))
	        {
		        fixedPath = fixedPath.Substring(this.rootHostUrl.Length - 1);	// Why "- 1"? Because I need to select "/"
	        }
			else if (fixedPath.StartsWith(this.rootProjectUrl))
            {
                fixedPath = fixedPath.Substring(this.rootProjectUrl.Length - 1); // Why "- 1"? Because I need to select "/"
            }

            var reMatch = Regex.Match(fixedPath, @"^/(?<container>[a-zA-Z\-_0-9\.]+)/(?<path>[0-9]{4,}(/.*)?)$");
            if (reMatch.Success)
            {
                containerName = reMatch.Groups["container"].Value;
                return reMatch.Groups["path"].Value;
            }

            containerName = this.ContainerName;
            return fixedPath;
        }

        /// <summary>
        /// Initialize Container
        /// </summary>
        protected void InitializeContainer()
        {
            var metadataCollection = new ContainerMetadataCollection(new IContainerMetadata[]
            {
                new AccessControlMaxAgeContainerMetadata() { MaxAgeSeconds = (long)(this.MaxDays * 24 * 3600) },
            });
            var responseTask = Task.Run(() => this.objectStorageService.SaveContainerMetadataAsync(this.ContainerName, metadataCollection));
            if (responseTask.Wait(Constants.WaitTaskTimeout) == false)
            {
                throw new TimeoutException("Unable to send command to server.");
            }
        }

        protected bool GetConfigValueBool(string configKey, bool fallbackValue = false)
        {
            var configValue = ConfigurationManager.AppSettings[configKey];
            if (string.IsNullOrEmpty(configValue))
            {
                return fallbackValue;
            }

            return configValue.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
#pragma warning restore SA1027 // Use tabs correctly
