using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.Projects;
using Azure.Identity;
using Azure.Storage.Blobs;
using DocumentChatbot.Functions.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// In the Azure Functions isolated worker model, local.settings.json Values are
// loaded as environment variables by `func start`. Read them via
// Environment.GetEnvironmentVariable, not IConfiguration.
static string Env(string key) =>
    Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException($"Missing required configuration: '{key}'");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Azure AI Foundry Agents client
        services.AddSingleton(_ =>
            new AIProjectClient(
                Env("AzureFoundry__ConnectionString"),
                new DefaultAzureCredential()));

        // Azure Blob Storage
        services.AddSingleton(_ =>
            new BlobServiceClient(Env("BlobStorage__ConnectionString")));

        // Azure Cosmos DB
        services.AddSingleton(_ =>
            new CosmosClient(Env("CosmosDB__ConnectionString")));

        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ICosmosRepository, CosmosRepository>();

        // Azure Document Intelligence (for OCR of scanned PDFs)
        services.AddSingleton(_ =>
            new DocumentIntelligenceClient(
                new Uri(Env("AzureDocumentIntelligence__Endpoint")),
                new AzureKeyCredential(Env("AzureDocumentIntelligence__Key"))));

        services.AddScoped<IOcrService, OcrService>();
    })
    .Build();

host.Run();
