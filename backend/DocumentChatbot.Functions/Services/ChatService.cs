using System.Text.RegularExpressions;
using Azure.AI.Projects;
using DocumentChatbot.Functions.Models;

namespace DocumentChatbot.Functions.Services;

public class ChatService : IChatService
{
    private static readonly Regex FoundryMarkerRegex = new(@"【\d+:\d+†[^】]*】", RegexOptions.Compiled);

    private readonly AIProjectClient _foundryClient;
    private readonly ICosmosRepository _cosmos;
    private readonly string _agentId;

    public ChatService(AIProjectClient foundryClient, ICosmosRepository cosmos)
    {
        _foundryClient = foundryClient;
        _cosmos = cosmos;
        _agentId = Environment.GetEnvironmentVariable("AzureFoundry__AgentId")
            ?? throw new InvalidOperationException("AzureFoundry__AgentId is not configured.");
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

        var result = new List<MessageDto>();
        foreach (var m in messagesResponse.Value.Data.OrderBy(m => m.CreatedAt))
        {
            var role = m.Role == MessageRole.Agent ? "assistant" : "user";
            var textContent = m.ContentItems.OfType<MessageTextContent>().FirstOrDefault();
            var text = CleanContent(textContent?.Text ?? string.Empty);
            var citations = role == "assistant"
                ? await ExtractCitationsAsync(agentsClient, m.ContentItems)
                : Array.Empty<DocumentCitation>();
            result.Add(new MessageDto(role, text, m.CreatedAt, citations));
        }
        return result;
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
            .Where(m => m.Role == MessageRole.Agent)
            .OrderByDescending(m => m.CreatedAt)
            .First();

        var textContent = assistantMessage.ContentItems.OfType<MessageTextContent>().FirstOrDefault();
        var responseText = CleanContent(textContent?.Text ?? string.Empty);
        var citations = await ExtractCitationsAsync(agentsClient, assistantMessage.ContentItems);

        return new SendMessageResponse(
            new MessageDto("assistant", responseText, assistantMessage.CreatedAt, citations));
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var session = await _cosmos.GetAsync<ChatSession>("sessions", sessionId);
        if (session is null) return;

        var agentsClient = _foundryClient.GetAgentsClient();
        try { await agentsClient.DeleteThreadAsync(session.ThreadId); } catch { }

        await _cosmos.DeleteAsync("sessions", sessionId);
    }

    private static string CleanContent(string text) =>
        FoundryMarkerRegex.Replace(text, string.Empty).Trim();

    private static async Task<IReadOnlyList<DocumentCitation>> ExtractCitationsAsync(
        AgentsClient agentsClient, IReadOnlyList<MessageContent> contentItems)
    {
        var textContent = contentItems.OfType<MessageTextContent>().FirstOrDefault();
        if (textContent is null) return Array.Empty<DocumentCitation>();

        var annotations = textContent.Annotations.OfType<MessageTextFileCitationAnnotation>().ToList();
        if (annotations.Count == 0) return Array.Empty<DocumentCitation>();

        var fullText = textContent.Text;

        // Group all annotations by FileId so we collect page numbers from every
        // occurrence of the same file, not just the first.
        var fileOrder = new List<string>();
        var pagesByFile = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);

        foreach (var annotation in annotations)
        {
            var fileId = annotation.FileId;
            if (!pagesByFile.ContainsKey(fileId))
            {
                pagesByFile[fileId] = new SortedSet<int>();
                fileOrder.Add(fileId);
            }

            // 1. Search the quoted chunk for "page N" patterns.
            CollectPageMatches(pagesByFile[fileId], annotation.Quote);

            // 2. Search the response text in the window immediately before this
            //    citation marker — the AI often says "on page 5..." right before
            //    inserting the inline reference marker.
            if (annotation.StartIndex.HasValue)
            {
                var windowStart = Math.Max(0, annotation.StartIndex.Value - 400);
                var context = fullText.Substring(windowStart, annotation.StartIndex.Value - windowStart);
                CollectPageMatches(pagesByFile[fileId], context);
            }
        }

        var citations = new List<DocumentCitation>();
        foreach (var fileId in fileOrder)
        {
            string fileName;
            try
            {
                var fileInfo = await agentsClient.GetFileAsync(fileId);
                fileName = fileInfo.Value.Filename;
            }
            catch
            {
                fileName = fileId; // fallback to ID if lookup fails
            }

            citations.Add(new DocumentCitation(fileName, pagesByFile[fileId].ToList()));
        }

        return citations;
    }

    private static void CollectPageMatches(SortedSet<int> pages, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Matches: "page 3", "pages 3-5", "pages 3–5", "page 3, 4"
        var matches = Regex.Matches(text, @"\bpage[s]?\s+(\d+)(?:\s*[-–,]\s*(\d+))?", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[1].Value, out var start))
            {
                if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var end))
                {
                    for (var p = start; p <= end; p++) pages.Add(p);
                }
                else
                {
                    pages.Add(start);
                }
            }
        }
    }
}
