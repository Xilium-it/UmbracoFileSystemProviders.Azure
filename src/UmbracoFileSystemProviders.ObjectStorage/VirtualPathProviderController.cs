// <copyright file="VirtualPathProviderController.cs" company="James Jackson-South, Jeavon Leopold, and contributors">
// Copyright (c) James Jackson-South, Jeavon Leopold, and contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace Our.Umbraco.FileSystemProviders.ObjectStorage
{
    using System;
	using System.Configuration;
    using global::Umbraco.Core;
    using global::Umbraco.Core.Configuration;

    /// <summary>
    /// Configures the virtual path provider to correctly retrieve and serve resources from the media section.
    /// </summary>
    public class VirtualPathProviderController : ApplicationEventHandler
    {
        /// <summary>
        /// Overridable method to execute when All resolvers have been initialized but resolution is not
        /// frozen so they can be modified in this method
        /// </summary>
        /// <param name="umbracoApplication">The current <see cref="UmbracoApplicationBase"/></param>
        /// <param name="applicationContext">The Umbraco <see cref="ApplicationContext"/> for the current application.</param>
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            var configSection = this.GetFileSystemProvidersConfigSection();
            foreach (FileSystemProviderElement providerConfig in configSection.Providers)
            {
                var providerType = Type.GetType(providerConfig.Type);
                if (providerType == null || providerType.IsAssignableFrom(typeof(ObjectStorageFileSystem)) == false)
				{
                    continue;
                }

                var virtualPathProperty = providerConfig.Parameters[Constants.FileSystemConfiguration.VirtualPathRouteKey];
                if (virtualPathProperty == null || string.IsNullOrEmpty(virtualPathProperty.Value))
                {
                    continue;
                }

                FileSystemVirtualPathProvider.Configure<ObjectStorageFileSystem>(providerConfig.Alias, virtualPathProperty.Value);
            }

            base.ApplicationStarting(umbracoApplication, applicationContext);
        }

        private FileSystemProvidersSection GetFileSystemProvidersConfigSection()
        {
            return (FileSystemProvidersSection) ConfigurationManager.GetSection("umbracoConfiguration/FileSystemProviders");
        }
    }
}
