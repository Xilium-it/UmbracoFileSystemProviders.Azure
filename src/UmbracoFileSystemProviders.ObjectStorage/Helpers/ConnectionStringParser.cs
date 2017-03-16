namespace Our.Umbraco.FileSystemProviders.ObjectStorage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Parser to encode/decode authentication connection string.
    /// </summary>
    internal class ConnectionStringParser
    {
        /// <summary>
        /// Create a ConnectionString to log in.
        /// </summary>
        /// <param name="connectionStringData">The connectionString data to encode.</param>
        /// <returns>Encoded connection string</returns>
        public string Encode(ConnectionStringModel connectionStringData)
        {
            return this.Encode(connectionStringData.UrlBase, connectionStringData.ProjectId, connectionStringData.Region, connectionStringData.Username, connectionStringData.Password);
        }

        /// <summary>
        /// Create a ConnectionString to log in.
        /// </summary>
        /// <param name="urlBase">The base URL for the cloud instance.</param>
        /// <param name="projectId">Identifier of project (alias tenant).</param>
        /// <param name="region">The region where is the service.</param>
        /// <param name="username">Username to log in.</param>
        /// <param name="password">Password to log in.</param>
        /// <returns>Encoded connection string</returns>
        public string Encode(string urlBase, string projectId, string region, string username, string password)
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder();

            builder.Add(Constants.ConnectionString.UrlBaseKey, urlBase);
            builder.Add(Constants.ConnectionString.ProjectIdKey, projectId);
            builder.Add(Constants.ConnectionString.RegionKey, region);
            builder.Add(Constants.ConnectionString.UsernameKey, username);
            builder.Add(Constants.ConnectionString.PasswordKey, password);

            return builder.ConnectionString;
        }

        /// <summary>
        /// Decode a connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to decode.</param>
        /// <returns>The model of connection string.</returns>
        public ConnectionStringModel Decode(string connectionString)
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder();
            var model = new ConnectionStringModel();

            builder.ConnectionString = connectionString;

            model.UrlBase = this.GetConnectionStringValue(builder, Constants.ConnectionString.UrlBaseKey);
            model.ProjectId = this.GetConnectionStringValue(builder, Constants.ConnectionString.ProjectIdKey);
            model.Region = this.GetConnectionStringValue(builder, Constants.ConnectionString.RegionKey, "");
            model.Username = this.GetConnectionStringValue(builder, Constants.ConnectionString.UsernameKey);
            model.Password = this.GetConnectionStringValue(builder, Constants.ConnectionString.PasswordKey);

            return model;
        }

        /// <summary>
        /// Get attribute value fom connection string builder.
        /// </summary>
        /// <param name="connectionStringBuilder">Builder where to read attribute.</param>
        /// <param name="attributeKey">Attribute to read.</param>
        /// <param name="fallbackValue">Fallback value where attribute does not exists.</param>
        /// <returns>Value of requested attribute.</returns>
        private string GetConnectionStringValue(System.Data.Common.DbConnectionStringBuilder connectionStringBuilder, string attributeKey, string fallbackValue = null)
        {
            object result;

            if (connectionStringBuilder.TryGetValue(attributeKey, out result) == false)
            {
                return fallbackValue;
            }

            return result.ToString();
        }
    }


}
