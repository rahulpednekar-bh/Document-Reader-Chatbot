namespace DocumentChatbot.Functions.Services;

public interface IOcrService
{
    /// <summary>
    /// Returns true if the PDF stream has no embedded text layer (i.e. is a scanned image PDF).
    /// The stream position is reset to 0 before returning.
    /// </summary>
    bool IsPdfScanned(Stream pdfStream);

    /// <summary>
    /// Runs Azure Document Intelligence OCR on the given PDF stream and returns a
    /// UTF-8 plain-text MemoryStream containing the extracted content.
    /// The stream position is reset to 0 before the call is made.
    /// </summary>
    Task<Stream> ExtractTextAsync(Stream pdfStream);
}
