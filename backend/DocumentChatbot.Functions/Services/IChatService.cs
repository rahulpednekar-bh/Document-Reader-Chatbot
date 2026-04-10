using DocumentChatbot.Functions.Models;

namespace DocumentChatbot.Functions.Services;

public interface IChatService
{
    Task<CreateSessionResponse> CreateSessionAsync(string? title);
    Task<IReadOnlyList<ChatSession>> ListSessionsAsync();
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(string sessionId);
    Task<SendMessageResponse> SendMessageAsync(string sessionId, string content);
}
