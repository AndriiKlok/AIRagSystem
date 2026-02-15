namespace CompanyKnowledgeAssistant.Core.Entities;

public class Chat
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public int MessageCount { get; set; } = 0;

    // Navigation properties
    public Area Area { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}