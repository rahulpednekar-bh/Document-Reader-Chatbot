namespace DocumentChatbot.Functions.Services;

public interface ICosmosRepository
{
    Task UpsertAsync<T>(string containerName, T item) where T : class;
    Task<T?> GetAsync<T>(string containerName, string id) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(string containerName, string query) where T : class;
    Task DeleteAsync(string containerName, string id);
}
