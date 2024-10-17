
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using DocumentSearcher.Models;

namespace DocumentSearcher.Extensions;

internal static class ISemanticMemoryClientExtensions
{
    private static readonly List<string> pipelineSteps = new() { "extract", "partition", "gen_embeddings", "save_records" };

    public static void AddSemanticMemoryServices(this WebApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.Services.BuildServiceProvider();

        var memoryConfig = serviceProvider.GetRequiredService<IOptions<KernelMemoryConfig>>().Value;

        var pipelineType = memoryConfig.DataIngestion.OrchestrationType;

        var memoryBuilder = new KernelMemoryBuilder(appBuilder.Services);

        IKernelMemory memory = memoryBuilder.FromMemoryConfiguration(
            memoryConfig,
            appBuilder.Configuration
        ).Build();

        appBuilder.Services.AddSingleton(memory);
    }

    public static IKernelMemoryBuilder FromMemoryConfiguration(
    this IKernelMemoryBuilder builder,
    KernelMemoryConfig memoryConfiguration,
    IConfiguration servicesConfiguration)
    {
        return new ServiceConfiguration(servicesConfiguration, memoryConfiguration).PrepareBuilder(builder);
    }


    public static Task<SearchResult> SearchMemoryAsync(
        this IKernelMemory memoryClient,
        string indexName,
        string query,
        float relevanceThreshold,
        string chatId,
        string? memoryName = null,
        CancellationToken cancellationToken = default)
    {
        return memoryClient.SearchMemoryAsync(indexName, query, relevanceThreshold, resultCount: -1, chatId, memoryName, cancellationToken);
    }

    public static async Task<SearchResult> SearchMemoryAsync(
        this IKernelMemory memoryClient,
        string indexName,
        string query,
        float relevanceThreshold,
        int resultCount,
        string chatId,
        string? memoryName = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new MemoryFilter();

        filter.ByTag(MemoryTags.TagChatId, chatId);

        if (!string.IsNullOrWhiteSpace(memoryName))
        {
            filter.ByTag(MemoryTags.TagMemory, memoryName);
        }

        var searchResult =
            await memoryClient.SearchAsync(
                query,
                indexName,
                filter,
                null,
                relevanceThreshold, // minRelevance param
                resultCount,
                cancellationToken: cancellationToken);

        return searchResult;
    }


}
