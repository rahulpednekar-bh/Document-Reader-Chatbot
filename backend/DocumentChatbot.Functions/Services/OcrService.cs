using Azure;
using Azure.AI.DocumentIntelligence;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocumentChatbot.Functions.Services;

public class OcrService : IOcrService
{
    /// <summary>
    /// Minimum number of characters across the first 5 pages to consider a PDF text-based.
    /// PDFs below this threshold are treated as scanned images.
    /// </summary>
    private const int ScannedTextThreshold = 50;
    private const int PagesToSample = 5;

    private readonly DocumentIntelligenceClient _docIntelligenceClient;

    public OcrService(DocumentIntelligenceClient docIntelligenceClient)
    {
        _docIntelligenceClient = docIntelligenceClient;
    }

    /// <inheritdoc />
    public bool IsPdfScanned(Stream pdfStream)
    {
        try
        {
            pdfStream.Position = 0;
            using var pdf = PdfDocument.Open(pdfStream, new ParsingOptions { UseLenientParsing = true });

            int totalChars = 0;
            int pagesToCheck = Math.Min(pdf.NumberOfPages, PagesToSample);

            for (int i = 1; i <= pagesToCheck; i++)
            {
                Page page = pdf.GetPage(i);
                totalChars += page.Text.Length;

                // Short-circuit: enough text found — definitely not scanned
                if (totalChars >= ScannedTextThreshold)
                    return false;
            }

            return totalChars < ScannedTextThreshold;
        }
        finally
        {
            // Always reset stream so callers can reuse it
            if (pdfStream.CanSeek)
                pdfStream.Position = 0;
        }
    }

    /// <inheritdoc />
    public async Task<Stream> ExtractTextAsync(Stream pdfStream)
    {
        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        // Read PDF bytes for the Document Intelligence API
        var pdfBytes = await StreamToBinaryDataAsync(pdfStream);

        // Call prebuilt-read model — performs OCR across all pages
        var operation = await _docIntelligenceClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            pdfBytes);

        var result = operation.Value;

        // Assemble extracted text page-by-page
        var sb = new StringBuilder();
        if (result.Pages is not null)
        {
            foreach (var page in result.Pages)
            {
                sb.AppendLine($"--- Page {page.PageNumber} ---");

                if (page.Lines is not null)
                {
                    foreach (var line in page.Lines)
                    {
                        sb.AppendLine(line.Content);
                    }
                }

                sb.AppendLine();
            }
        }

        var textBytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new MemoryStream(textBytes);
    }

    private static async Task<BinaryData> StreamToBinaryDataAsync(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return BinaryData.FromBytes(ms.ToArray());
    }
}
