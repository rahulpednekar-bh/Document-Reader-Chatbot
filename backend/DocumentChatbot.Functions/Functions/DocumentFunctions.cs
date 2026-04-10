using System.Net;
using System.Text.Json;
using DocumentChatbot.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
    public async Task<HttpResponseData> UploadAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues) ||
                !contentTypeValues.First().StartsWith("multipart/form-data"))
            {
                return await BadRequest(req, "Request must be multipart/form-data.");
            }

            var form = await req.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null)
                return await BadRequest(req, "No file was included in the request.");

            await using var stream = file.OpenReadStream();
            var result = await _documentService.UploadAsync(stream, file.FileName, file.Length);

            return await Ok(req, result);
        }
        catch (ArgumentException ex)
        {
            return await BadRequest(req, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document upload.");
            return await Error(req, "An unexpected error occurred.");
        }
    }

    [Function("ListDocuments")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req)
    {
        var documents = await _documentService.ListAsync();
        return await Ok(req, documents);
    }

    [Function("GetDocument")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/{id}")] HttpRequestData req,
        string id)
    {
        var document = await _documentService.GetAsync(id);
        if (document is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await Ok(req, document);
    }

    private static async Task<HttpResponseData> Ok(HttpRequestData req, object body)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions));
        return response;
    }

    private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { error = message }));
        return response;
    }

    private static async Task<HttpResponseData> Error(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(new { error = message }));
        return response;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
