using System.Net;
using System.Text.Json;
using DocumentChatbot.Functions.Models;
using DocumentChatbot.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentChatbot.Functions.Functions;

public class DocumentFunctions
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentFunctions> _logger;

    public DocumentFunctions(IDocumentService documentService, ILogger<DocumentFunctions> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [Function("UploadDocument")]
    public async Task<IActionResult> UploadAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequest req)
    {
        try
        {
            if (!req.ContentType?.StartsWith("multipart/form-data") ?? true)
                return new BadRequestObjectResult(new { error = "Request must be multipart/form-data." });

            var file = req.Form.Files.FirstOrDefault();
            if (file is null)
                return new BadRequestObjectResult(new { error = "No file was included in the request." });

            await using var stream = file.OpenReadStream();
            var result = await _documentService.UploadAsync(stream, file.FileName, file.Length);

            return new OkObjectResult(result);
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document upload.");
            return new ObjectResult(new { error = "An unexpected error occurred." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    [Function("ListDocuments")]
    public async Task<IActionResult> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequest req)
    {
        var documents = await _documentService.ListAsync();
        return new OkObjectResult(documents);
    }

    [Function("GetDocument")]
    public async Task<IActionResult> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/{id}")] HttpRequest req,
        string id)
    {
        var document = await _documentService.GetAsync(id);
        if (document is null)
            return new NotFoundResult();

        return new OkObjectResult(document);
    }

    [Function("DeleteDocuments")]
    public async Task<IActionResult> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "documents")] HttpRequest req)
    {
        try
        {
            var body = await req.ReadFromJsonAsync<DeleteDocumentsRequest>();
            if (body is null || body.DocumentIds.Count == 0)
                return new BadRequestObjectResult(new { error = "At least one document ID is required." });

            await _documentService.DeleteAsync(body.DocumentIds);
            return new OkResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document deletion.");
            return new ObjectResult(new { error = "An unexpected error occurred." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
