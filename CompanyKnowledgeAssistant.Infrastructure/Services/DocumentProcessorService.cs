using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CompanyKnowledgeAssistant.Infrastructure.Services;

public class DocumentProcessorService
{
    public string ExtractText(string filePath, string fileType)
    {
        return fileType.ToLower() switch
        {
            "pdf" => ExtractTextFromPdf(filePath),
            "docx" => ExtractTextFromDocx(filePath),
            "txt" or "md" => ExtractTextFromTxt(filePath),
            _ => throw new NotSupportedException($"Unsupported file type: {fileType}")
        };
    }

    private string ExtractTextFromPdf(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var text = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
            text.Append(page.Text);
            text.AppendLine();
        }

        return text.ToString();
    }

    private string ExtractTextFromDocx(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document.Body;
        return body?.InnerText ?? string.Empty;
    }

    private string ExtractTextFromTxt(string filePath)
    {
        return File.ReadAllText(filePath, System.Text.Encoding.UTF8);
    }

    public List<ProcessedChunk> SplitIntoChunks(string text, int chunkSize = 600, int overlap = 100)
    {
        var sentences = SplitIntoSentences(text);
        var chunks = new List<ProcessedChunk>();
        var currentChunk = string.Empty;
        var chunkIndex = 0;

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(new ProcessedChunk
                {
                    Content = currentChunk.Trim(),
                    ChunkIndex = chunkIndex++
                });

                currentChunk = GetOverlapText(currentChunk, overlap) + " " + sentence;
            }
            else
            {
                currentChunk += " " + sentence;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(new ProcessedChunk
            {
                Content = currentChunk.Trim(),
                ChunkIndex = chunkIndex
            });
        }

        return chunks;
    }

    private List<string> SplitIntoSentences(string text)
    {
        var pattern = @"(?<=[.!?])\s+(?=[A-Z])";
        var sentences = System.Text.RegularExpressions.Regex.Split(text, pattern);
        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private string GetOverlapText(string text, int overlapSize)
    {
        if (text.Length <= overlapSize)
            return text;

        var overlapText = text.Substring(text.Length - overlapSize);
        var lastSpaceIndex = overlapText.LastIndexOf(' ');
        if (lastSpaceIndex > 0)
        {
            overlapText = overlapText.Substring(lastSpaceIndex + 1);
        }

        return overlapText;
    }
}

public class ProcessedChunk
{
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public float[]? Embedding { get; set; }
}