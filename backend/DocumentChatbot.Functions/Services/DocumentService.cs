using Azure.AI.Projects;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentChatbot.Functions.Models;

namespace DocumentChatbot.Functions.Services;

public class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".docx"];
    private const long MaxSizeBytes = 25 * 1024 * 1024; // 25 MB

    private readonly BlobServiceClient _blobServiceClient;
    private readonly AIProjectClient _foundryClient;
    private readonly ICosmosRepository _cosmos;
    private readonly IOcrService _ocrService;
    private readonly string _containerName;
    private readonly string _vectorStoreId;

    public DocumentService(
        BlobServiceClient blobServiceClient,
        AIProjectClient foundryClient,
        ICosmosRepository cosmos,
        IOcrService ocrService)
    {
        _blobServiceClient = blobServiceClient;
        _foundryClient = foundryClient;
        _cosmos = cosmos;
        _ocrService = ocrService;
        _containerName = Environment.GetEnvironmentVariable("BlobStorage__ContainerName")
            ?? throw new InvalidOperationException("BlobStorage__ContainerName is not configured.");
        _vectorStoreId = Environment.GetEnvironmentVariable("AzureFoundry__VectorStoreId")
            ?? throw new InvalidOperationException("AzureFoundry__VectorStoreId is not configured.");
    }

    public async Task<UploadDocumentResponse> UploadAsync(Stream fileStream, string fileName, long sizeBytes)
    {
        ValidateFile(fileName, sizeBytes);

        var documentId = Guid.NewGuid().ToString();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // --- Step 1: Store the original file in Blob Storage (always preserved) ---
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient($"{documentId}/{fileName}");
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(fileName) }
        });
        var blobUrl = blobClient.Uri.ToString();

        // --- Step 2: Determine what to send to Foundry (raw file or OCR text) ---
        bool ocrApplied = false;
        string? processingNote = null;
        Stream foundryStream;
        string foundryFileName;

        if (extension == ".pdf")
        {
            fileStream.Position = 0;
            bool isScanned = _ocrService.IsPdfScanned(fileStream);

            if (isScanned)
            {
                try
                {
                    // Run OCR — stream is reset inside IsPdfScanned already
                    foundryStream = await _ocrService.ExtractTextAsync(fileStream);
                    foundryFileName = Path.GetFileNameWithoutExtension(fileName) + "_ocr.txt";
                    ocrApplied = true;
                    processingNote = "Scanned PDF detected. Text was extracted via OCR before indexing.";
                }
                catch (Exception ex)
                {
                    // OCR failed — persist failure record and surface a clear error
                    var failedMetadata = new DocumentMetadata
                    {
                        Id = documentId,
                        FileName = fileName,
                        BlobUrl = blobUrl,
                        FoundryFileId = string.Empty,
                        Status = "failed",
                        SizeBytes = sizeBytes,
                        OcrApplied = false,
                        ProcessingNote = $"OCR processing failed: {ex.Message}"
                    };
                    await _cosmos.UpsertAsync("documents", failedMetadata);

                    throw new InvalidOperationException(
                        $"This PDF appears to be a scanned image but OCR processing failed: {ex.Message}", ex);
                }
            }
            else
            {
                // Normal text-based PDF — upload as-is
                fileStream.Position = 0;
                foundryStream = fileStream;
                foundryFileName = fileName;
            }
        }
        else
        {
            // DOCX — always upload as-is
            fileStream.Position = 0;
            foundryStream = fileStream;
            foundryFileName = fileName;
        }

        // --- Step 3: Upload to Azure AI Foundry Files API ---
        var agentsClient = _foundryClient.GetAgentsClient();
        var uploadedFile = await agentsClient.UploadFileAsync(foundryStream, AgentFilePurpose.Agents, foundryFileName);

        // --- Step 4: Attach file to the shared Vector Store ---
        await agentsClient.CreateVectorStoreFileAsync(_vectorStoreId, uploadedFile.Value.Id);

        // --- Step 5: Persist metadata to Cosmos DB ---
        var metadata = new DocumentMetadata
        {
            Id = documentId,
            FileName = fileName,
            BlobUrl = blobUrl,
            FoundryFileId = uploadedFile.Value.Id,
            Status = "indexed",
            SizeBytes = sizeBytes,
            OcrApplied = ocrApplied,
            ProcessingNote = processingNote
        };

        await _cosmos.UpsertAsync("documents", metadata);

        return new UploadDocumentResponse(documentId, metadata.Status, fileName, ocrApplied, processingNote);
    }

    public async Task<IReadOnlyList<DocumentMetadata>> ListAsync() =>
        await _cosmos.QueryAsync<DocumentMetadata>("documents",
            "SELECT * FROM c ORDER BY c.uploadedAt DESC");

    public async Task<DocumentMetadata?> GetAsync(string documentId) =>
        await _cosmos.GetAsync<DocumentMetadata>("documents", documentId);

    public async Task DeleteAsync(IReadOnlyList<string> documentIds)
    {
        var agentsClient = _foundryClient.GetAgentsClient();
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

        foreach (var id in documentIds)
        {
            var metadata = await _cosmos.GetAsync<DocumentMetadata>("documents", id);
            if (metadata is null) continue;

            // Remove from Foundry Vector Store
            if (!string.IsNullOrEmpty(metadata.FoundryFileId))
            {
                try
                {
                    await agentsClient.DeleteVectorStoreFileAsync(_vectorStoreId, metadata.FoundryFileId);
                }
                catch (Exception)
                {
                    // File may not be in the vector store; continue cleanup
                }

                // Delete from Foundry Files API
                try
                {
                    await agentsClient.DeleteFileAsync(metadata.FoundryFileId);
                }
                catch (Exception)
                {
                    // File may already be deleted; continue cleanup
                }
            }

            // Delete blob from Azure Blob Storage
            var blobClient = containerClient.GetBlobClient($"{id}/{metadata.FileName}");
            await blobClient.DeleteIfExistsAsync();

            // Delete metadata from Cosmos DB (last, so record persists if earlier steps fail)
            await _cosmos.DeleteAsync("documents", id);
        }
    }

    private static void ValidateFile(string fileName, long sizeBytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException($"File type '{extension}' is not allowed. Only .pdf and .docx are accepted.");

        if (sizeBytes > MaxSizeBytes)
            throw new ArgumentException($"File size {sizeBytes} bytes exceeds the 25 MB limit.");
    }

    private static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
}
