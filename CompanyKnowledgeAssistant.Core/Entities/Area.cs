namespace CompanyKnowledgeAssistant.Core.Entities;

public class Area
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int DocumentCount { get; set; } = 0;
    public int ChatCount { get; set; } = 0;

    // Navigation properties
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Chat> Chats { get; set; } = new List<Chat>();
}