using Newtonsoft.Json;

namespace DocumentChatbot.Functions.Models;

public class DocumentMetadata
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("blobUrl")]
    public string BlobUrl { get; set; } = string.Empty;

    [JsonProperty("foundryFileId")]
    public string FoundryFileId { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = "indexed";

    [JsonProperty("uploadedAt")]
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonProperty("sizeBytes")]
    public long SizeBytes { get; set; }
}
