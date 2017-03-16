// <copyright file="Constants.cs" company="James Jackson-South, Jeavon Leopold, and contributors">
// Copyright (c) James Jackson-South, Jeavon Leopold, and contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace Our.Umbraco.FileSystemProviders.ObjectStorage
{
    /// <summary>
    /// Constant strings for use within the application.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The default route path for media objects.
        /// </summary>
        public const string DefaultMediaRoute = "media";

        /// <summary>
        /// The default MaxDays value for browser cache.
        /// </summary>
        public const int DefaultMaxDays = 365;

        /// <summary>
        /// The default UseDefaultRoute value.
        /// </summary>
        public const bool DefaultUseDefRoute = true;

        /// <summary>
        /// The default UsePrivateContainer value.
        /// </summary>
        public const bool DefaultUsePrivateContainer = true;

        /// <summary>
        /// Timeout for wait task call (milliseconds)
        /// </summary>
        internal const int WaitTaskTimeout = 20 * 1000;

        /// <summary>
        /// Timeout for wait task call with stream in/ou (milliseconds)
        /// </summary>
        internal const int WaitStreamTaskTimeout = 24 * 3600 * 1000;

        /// <summary>
        /// The configuration setting constants.
        /// </summary>
        public static class Configuration
        {
            /// <summary>
            /// The configuration key for providing the ObjectStorage Blob Container Name via the web.config
            /// </summary>
            public const string ContainerNameKey = "ObjectStorageBlobFileSystem.ContainerName";

            /// <summary>
            /// The configuration key for providing the Root URL via the web.config
            /// </summary>
            public const string RootUrlKey = "ObjectStorageBlobFileSystem.RootUrl";

            /// <summary>
            /// The configuration key for providing the ConnectionString via the web.config
            /// </summary>
            public const string ConnectionStringKey = "ObjectStorageBlobFileSystem.ConnectionString";

            /// <summary>
            /// The configuration key for providing the Maximum Days Cache value via the web.config
            /// </summary>
            public const string MaxDaysKey = "ObjectStorageBlobFileSystem.MaxDays";

            /// <summary>
            /// The configuration key for providing the Use Default Root value via the web.config
            /// </summary>
            public const string UseDefaultRouteKey = "ObjectStorageBlobFileSystem.UseDefaultRoute";

            /// <summary>
            /// The configuration key for disabling the virtual path provider.
            /// </summary>
            public const string DisableVirtualPathProviderKey = "ObjectStorageBlobFileSystem.DisableVirtualPathProvider";

            /// <summary>
            /// The configuration key for enabling the storage emulator.
            /// </summary>
            public const string UseStorageEmulatorKey = "ObjectStorageBlobFileSystem.UseStorageEmulator";

            /// <summary>
            /// The configuration key for providing the Use Private Container value via the web.config
            /// </summary>
            public const string UsePrivateContainer = "ObjectStorageBlobFileSystem.UsePrivateContainer";
        }

        /// <summary>
        /// The connection string arguments.
        /// </summary>
        public static class ConnectionString
        {
            /// <summary>
            /// The UrlBase attribute name.
            /// </summary>
            public const string UrlBaseKey = "urlBase";

            /// <summary>
            /// The Project identifier attribute name.
            /// </summary>
            public const string ProjectIdKey = "projectId";

            /// <summary>
            /// The Region attribute name.
            /// </summary>
            public const string RegionKey = "region";

            /// <summary>
            /// The Username attribute name.
            /// </summary>
            public const string UsernameKey = "username";

            /// <summary>
            /// The Password attribute name.
            /// </summary>
            public const string PasswordKey = "password";
        }
    }
}
