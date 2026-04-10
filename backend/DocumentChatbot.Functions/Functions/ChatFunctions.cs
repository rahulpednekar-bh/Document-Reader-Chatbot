using System.Net;
using System.Text.Json;
using DocumentChatbot.Functions.Models;
using DocumentChatbot.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DocumentChatbot.Functions.Functions;

public class ChatFunctions
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatFunctions> _logger;

    public ChatFunctions(IChatService chatService, ILogger<ChatFunctions> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [Function("CreateSession")]
    public async Task<HttpResponseData> CreateSessionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions")] HttpRequestData req)
    {
        try
        {
            var body = await req.ReadFromJsonAsync<CreateSessionRequest>();
            var result = await _chatService.CreateSessionAsync(body?.Title);
            return await Created(req, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session.");
            return await Error(req, "Failed to create session.");
        }
    }

    [Function("ListSessions")]
    public async Task<HttpResponseData> ListSessionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions")] HttpRequestData req)
    {
        var sessions = await _chatService.ListSessionsAsync();
        return await Ok(req, sessions);
    }

    [Function("GetSessionMessages")]
    public async Task<HttpResponseData> GetMessagesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{sessionId}/messages")] HttpRequestData req,
        string sessionId)
    {
        try
        {
            var messages = await _chatService.GetMessagesAsync(sessionId);
            return await Ok(req, messages);
        }
        catch (KeyNotFoundException)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
    }

    [Function("SendMessage")]
    public async Task<HttpResponseData> SendMessageAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions/{sessionId}/messages")] HttpRequestData req,
        string sessionId)
    {
        try
        {
            var body = await req.ReadFromJsonAsync<SendMessageRequest>();
            if (body is null || string.IsNullOrWhiteSpace(body.Content))
                return await BadRequest(req, "Message content is required.");

            var result = await _chatService.SendMessageAsync(sessionId, body.Content);
            return await Ok(req, result);
        }
        catch (KeyNotFoundException)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return await Error(req, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to session {SessionId}.", sessionId);
            return await Error(req, "Failed to send message.");
        }
    }

    private static async Task<HttpResponseData> Ok(HttpRequestData req, object body)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions));
        return response;
    }

    private static async Task<HttpResponseData> Created(HttpRequestData req, object body)
    {
        var response = req.CreateResponse(HttpStatusCode.Created);
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
