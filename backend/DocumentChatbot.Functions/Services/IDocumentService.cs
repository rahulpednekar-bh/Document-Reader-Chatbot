using DocumentChatbot.Functions.Models;

namespace DocumentChatbot.Functions.Services;

public interface IDocumentService
{
    Task<UploadDocumentResponse> UploadAsync(Stream fileStream, string fileName, long sizeBytes);
    Task<IReadOnlyList<DocumentMetadata>> ListAsync();
    Task<DocumentMetadata?> GetAsync(string documentId);
    Task DeleteAsync(IReadOnlyList<string> documentIds);
}
