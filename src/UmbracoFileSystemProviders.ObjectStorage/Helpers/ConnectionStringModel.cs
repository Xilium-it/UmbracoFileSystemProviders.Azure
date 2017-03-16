namespace Our.Umbraco.FileSystemProviders.ObjectStorage {

    /// <summary>
    /// ConnectionString data
    /// </summary>
    internal class ConnectionStringModel {

        /// <summary>
        /// Gets or Sets the base URL for the cloud instance.
        /// </summary>
        public string UrlBase { get; set; }

        /// <summary>
        /// Gets or Sets the Identifier of project (alias tenant).
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Gets or Sets the region where is the service.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Gets or Sets the Username to log in.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or Sets the Password to log in.
        /// </summary>
        public string Password { get; set; }

    }
}
