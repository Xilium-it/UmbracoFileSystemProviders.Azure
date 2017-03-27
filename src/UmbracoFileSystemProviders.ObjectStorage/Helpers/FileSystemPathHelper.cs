namespace Our.Umbraco.FileSystemProviders.ObjectStorage {

using System;

	/// <summary>
	/// Helper for FileSystem Path
	/// </summary>
	internal class FileSystemPathHelper {

		/// <summary>
		/// Returns SingleTone of FileSystemPathHelper
		/// </summary>
		private static Lazy<FileSystemPathHelper> instance = new Lazy<FileSystemPathHelper>(CreateInstance);

		private FileSystemPathHelper()
		{
		}

		private static FileSystemPathHelper CreateInstance()
		{
			return new FileSystemPathHelper();
		}

		public static FileSystemPathHelper Instance
		{
			get { return instance.Value; }
		}

		/// <summary>
		/// Path delimiter
		/// </summary>
		public const string Delimiter = "/";

		/// <summary>
		/// Returns path with <see cref="Delimiter"/> char as last char.
		/// </summary>
		/// <param name="path">Path to work</param>
		/// <returns>Worked path</returns>
		public string PathWithDelimiter(string path)
		{
			if (path.EndsWith(Delimiter))
			{
				return path;
			}

			return path + Delimiter;
		}

		/// <summary>
		/// Returns path without <see cref="Delimiter"/> char as last char.
		/// </summary>
		/// <param name="path">Path to work</param>
		/// <returns>Worked path</returns>
		public string PathWithoutDelimiter(string path)
		{
			if (path.EndsWith(Delimiter) == false)
			{
				return path;
			}

			return path.TrimEnd(Delimiter[0]);
		}

		/// <inheritdoc cref="System.IO.Path.GetFileName"/>
		public string FileNameWithExt(string path)
		{
			return System.IO.Path.GetFileName(path);
		}
	}
}
