namespace CompanyKnowledgeAssistant.Core.Entities;

public class Document
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string ProcessingStatus { get; set; } = "Uploading";
    public int ChunkCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public Area Area { get; set; } = null!;
    public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();
}