using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace DocumentChatbot.Functions.Services;

public class CosmosRepository : ICosmosRepository
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;

    public CosmosRepository(CosmosClient client, IConfiguration config)
    {
        _client = client;
        _databaseName = config["CosmosDB__DatabaseName"]!;
    }

    public async Task UpsertAsync<T>(string containerName, T item) where T : class
    {
        var container = _client.GetContainer(_databaseName, containerName);
        await container.UpsertItemAsync(item);
    }

    public async Task<T?> GetAsync<T>(string containerName, string id) where T : class
    {
        var container = _client.GetContainer(_databaseName, containerName);
        try
        {
            var response = await container.ReadItemAsync<T>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string containerName, string query) where T : class
    {
        var container = _client.GetContainer(_databaseName, containerName);
        var iterator = container.GetItemQueryIterator<T>(query);
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page);
        }
        return results;
    }
}
