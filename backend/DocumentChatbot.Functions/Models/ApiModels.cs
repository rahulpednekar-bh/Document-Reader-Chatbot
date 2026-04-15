namespace DocumentChatbot.Functions.Models;

public record UploadDocumentResponse(string DocumentId, string Status, string FileName);

public record CreateSessionRequest(string? Title);

public record CreateSessionResponse(string Id, string Title, DateTimeOffset CreatedAt);

public record DeleteDocumentsRequest(IReadOnlyList<string> DocumentIds);

public record SendMessageRequest(string Content);

public record DocumentCitation(string FileName, IReadOnlyList<int> PageNumbers);

public record MessageDto(string Role, string Content, DateTimeOffset CreatedAt, IReadOnlyList<DocumentCitation> Citations);

public record SendMessageResponse(MessageDto AssistantMessage);
