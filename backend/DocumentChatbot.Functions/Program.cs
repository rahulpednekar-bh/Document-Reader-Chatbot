using Azure.AI.Projects;
using Azure.Identity;
using Azure.Storage.Blobs;
using DocumentChatbot.Functions.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Azure AI Foundry Agents client
        services.AddSingleton(_ =>
            new AIProjectClient(
                config["AzureFoundry__ConnectionString"]!,
                new DefaultAzureCredential()));

        // Azure Blob Storage
        services.AddSingleton(_ =>
            new BlobServiceClient(config["BlobStorage__ConnectionString"]));

        // Azure Cosmos DB
        services.AddSingleton(_ =>
            new CosmosClient(config["CosmosDB__ConnectionString"]));

        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ICosmosRepository, CosmosRepository>();
    })
    .Build();

host.Run();
