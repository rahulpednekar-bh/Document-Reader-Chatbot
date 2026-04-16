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

    /// <summary>
    /// True when the document was a scanned PDF and was processed through OCR
    /// before being indexed. The text extracted by OCR was uploaded to Foundry.
    /// </summary>
    [JsonProperty("ocrApplied")]
    public bool OcrApplied { get; set; }

    /// <summary>
    /// Human-readable note attached when processing fails or when OCR was applied.
    /// Null for normal text-based documents.
    /// </summary>
    [JsonProperty("processingNote")]
    public string? ProcessingNote { get; set; }
}
