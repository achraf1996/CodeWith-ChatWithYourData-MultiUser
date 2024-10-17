// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Pipeline.Queue.DevTools;

namespace DocumentSearcher.Extensions;

internal sealed class ServiceConfiguration
{
    // Content of appsettings.json, used to access dynamic data under "Services"
    private IConfiguration _rawAppSettings;

    // Normalized configuration
    private KernelMemoryConfig _memoryConfiguration;

    // appsettings.json root node name
    private const string ConfigRoot = "KernelMemory";

    // ASP.NET env var
    private const string AspnetEnvVar = "ASPNETCORE_ENVIRONMENT";

    // OpenAI env var
    private const string OpenAIEnvVar = "OPENAI_API_KEY";

    public ServiceConfiguration(string? settingsDirectory = null)
        : this(ReadAppSettings(settingsDirectory))
    {
    }

    public ServiceConfiguration(IConfiguration rawAppSettings)
        : this(rawAppSettings,
            rawAppSettings.GetSection(ConfigRoot).Get<KernelMemoryConfig>()
            ?? throw new ConfigurationException($"Unable to load Kernel Memory settings from the given configuration. " +
                                                $"There should be a '{ConfigRoot}' root node, " +
                                                $"with data mapping to '{nameof(KernelMemoryConfig)}'"))
    {
    }

    public ServiceConfiguration(
        IConfiguration rawAppSettings,
        KernelMemoryConfig memoryConfiguration)
    {
        this._rawAppSettings = rawAppSettings ?? throw new ConfigurationException("The given app settings configuration is NULL");
        this._memoryConfiguration = memoryConfiguration ?? throw new ConfigurationException("The given memory configuration is NULL");
    }

    public IKernelMemoryBuilder PrepareBuilder(IKernelMemoryBuilder builder)
    {
        return this.BuildUsingConfiguration(builder);
    }

    private IKernelMemoryBuilder BuildUsingConfiguration(IKernelMemoryBuilder builder)
    {
        if (this._memoryConfiguration == null)
        {
            throw new ConfigurationException("The given memory configuration is NULL");
        }

        if (this._rawAppSettings == null)
        {
            throw new ConfigurationException("The given app settings configuration is NULL");
        }

        builder.AddSingleton(this._memoryConfiguration);

        this.ConfigureSearchClient(builder);

        this.ConfigureRetrievalMemoryDb(builder);

        return builder;
    }

    private static IConfiguration ReadAppSettings(string? settingsDirectory)
    {
        var builder = new ConfigurationBuilder();
        builder.AddKMConfigurationSources(settingsDirectory: settingsDirectory);
        return builder.Build();
    }

    private void ConfigureSearchClient(IKernelMemoryBuilder builder)
    {
        // Search settings
        builder.WithSearchClientConfig(this._memoryConfiguration.Retrieval.SearchClient);
    }

    private void ConfigureRetrievalMemoryDb(IKernelMemoryBuilder builder)
    {
        // Retrieval Memory DB - IMemoryDb interface
        switch (this._memoryConfiguration.Retrieval.MemoryDbType)
        {
            case string x when x.Equals("AzureAISearch", StringComparison.OrdinalIgnoreCase):
                builder.Services.AddAzureAISearchAsMemoryDb(this.GetServiceConfig<AzureAISearchConfig>("AzureAISearch"));
                break;

            default:
                // NOOP - allow custom implementations, via WithCustomMemoryDb()
                break;
        }
    }


    /// <summary>
    /// Get an instance of T, using dependencies available in the builder,
    /// except for existing service descriptors for T. Replace/Use the
    /// given action to define T's implementation.
    /// Return an instance of T built using the definition provided by
    /// the action.
    /// </summary>
    /// <param name="builder">KM builder</param>
    /// <param name="addCustomService">Action used to configure the service collection</param>
    /// <typeparam name="T">Target type/interface</typeparam>
    private T GetServiceInstance<T>(IKernelMemoryBuilder builder, Action<IServiceCollection> addCustomService)
    {
        // Clone the list of service descriptors, skipping T descriptor
        IServiceCollection services = new ServiceCollection();
        foreach (ServiceDescriptor d in builder.Services)
        {
            if (d.ServiceType == typeof(T)) { continue; }

            services.Add(d);
        }

        // Add the custom T descriptor
        addCustomService.Invoke(services);

        // Build and return an instance of T, as defined by `addCustomService`
        return services.BuildServiceProvider().GetService<T>()
               ?? throw new ConfigurationException($"Unable to build {nameof(T)}");
    }

    /// <summary>
    /// Read a dependency configuration from IConfiguration
    /// Data is usually retrieved from KernelMemory:Services:{serviceName}, e.g. when using appsettings.json
    /// {
    ///   "KernelMemory": {
    ///     "Services": {
    ///       "{serviceName}": {
    ///         ...
    ///         ...
    ///       }
    ///     }
    ///   }
    /// }
    /// </summary>
    /// <param name="serviceName">Name of the dependency</param>
    /// <typeparam name="T">Type of configuration to return</typeparam>
    /// <returns>Configuration instance, settings for the dependency specified</returns>
    private T GetServiceConfig<T>(string serviceName)
    {
        return this._memoryConfiguration.GetServiceConfig<T>(this._rawAppSettings, serviceName);
    }
}


