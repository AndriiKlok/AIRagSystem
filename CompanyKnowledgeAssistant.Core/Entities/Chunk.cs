namespace CompanyKnowledgeAssistant.Core.Entities;

public class Chunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
    public string? Metadata { get; set; }

    // Navigation property
    public Document Document { get; set; } = null!;
}