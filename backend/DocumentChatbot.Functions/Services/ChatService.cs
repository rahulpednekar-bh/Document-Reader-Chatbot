using Azure.AI.Projects;
using DocumentChatbot.Functions.Models;
using Microsoft.Extensions.Configuration;

namespace DocumentChatbot.Functions.Services;

public class ChatService : IChatService
{
    private readonly AIProjectClient _foundryClient;
    private readonly ICosmosRepository _cosmos;
    private readonly string _agentId;

    public ChatService(AIProjectClient foundryClient, ICosmosRepository cosmos, IConfiguration config)
    {
        _foundryClient = foundryClient;
        _cosmos = cosmos;
        _agentId = config["AzureFoundry__AgentId"]!;
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(string? title)
    {
        var agentsClient = _foundryClient.GetAgentsClient();
        var thread = await agentsClient.CreateThreadAsync();

        var session = new ChatSession
        {
            ThreadId = thread.Value.Id,
            Title = string.IsNullOrWhiteSpace(title) ? "New Chat" : title
        };

        await _cosmos.UpsertAsync("sessions", session);

        return new CreateSessionResponse(session.Id, session.Title, session.CreatedAt);
    }

    public async Task<IReadOnlyList<ChatSession>> ListSessionsAsync() =>
        await _cosmos.QueryAsync<ChatSession>("sessions",
            "SELECT * FROM c ORDER BY c.createdAt DESC");

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(string sessionId)
    {
        var session = await _cosmos.GetAsync<ChatSession>("sessions", sessionId)
            ?? throw new KeyNotFoundException($"Session '{sessionId}' not found.");

        var agentsClient = _foundryClient.GetAgentsClient();
        var messagesResponse = await agentsClient.GetMessagesAsync(session.ThreadId);

        return messagesResponse.Value.Data
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageDto(
                m.Role.ToString().ToLower(),
                m.ContentItems.OfType<MessageTextContent>().FirstOrDefault()?.Text.Value ?? string.Empty,
                m.CreatedAt))
            .ToList();
    }

    public async Task<SendMessageResponse> SendMessageAsync(string sessionId, string content)
    {
        var session = await _cosmos.GetAsync<ChatSession>("sessions", sessionId)
            ?? throw new KeyNotFoundException($"Session '{sessionId}' not found.");

        var agentsClient = _foundryClient.GetAgentsClient();

        await agentsClient.CreateMessageAsync(session.ThreadId, MessageRole.User, content);

        var run = await agentsClient.CreateRunAsync(session.ThreadId, _agentId);

        // Poll until the run reaches a terminal state
        while (run.Value.Status == RunStatus.Queued || run.Value.Status == RunStatus.InProgress)
        {
            await Task.Delay(500);
            run = await agentsClient.GetRunAsync(session.ThreadId, run.Value.Id);
        }

        if (run.Value.Status != RunStatus.Completed)
            throw new InvalidOperationException($"Run ended with status: {run.Value.Status}");

        var messages = await agentsClient.GetMessagesAsync(session.ThreadId);
        var assistantMessage = messages.Value.Data
            .Where(m => m.Role == MessageRole.Assistant)
            .OrderByDescending(m => m.CreatedAt)
            .First();

        var responseText = assistantMessage.ContentItems
            .OfType<MessageTextContent>()
            .FirstOrDefault()?.Text.Value ?? string.Empty;

        return new SendMessageResponse(
            new MessageDto("assistant", responseText, assistantMessage.CreatedAt));
    }
}
