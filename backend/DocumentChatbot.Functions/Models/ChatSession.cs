using Newtonsoft.Json;

namespace DocumentChatbot.Functions.Models;

public class ChatSession
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("title")]
    public string Title { get; set; } = "New Chat";

    [JsonProperty("threadId")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
