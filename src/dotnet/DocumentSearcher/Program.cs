using DocumentSearcher.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.KernelMemory;

var builder = WebApplication.CreateBuilder(args);

builder.AddSemanticMemoryServices();

var app = builder.Build();

// Define the /search endpoint
app.MapGet("/search", async (IKernelMemory memoryClient, string query, string chatId) =>
{
    // Example usage of the memoryClient with the extension method
    var result = await memoryClient.SearchMemoryAsync(
        indexName: "your-index-name",
        query: query,
        relevanceThreshold: 0.5f,
        chatId: chatId
    );

    // Return the search result
    return Results.Ok(result);
});


// Run the application
app.Run();