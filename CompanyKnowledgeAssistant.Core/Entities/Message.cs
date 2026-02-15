namespace CompanyKnowledgeAssistant.Core.Entities;

public class Message
{
    public int Id { get; set; }
    public int ChatId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty; // Plain text
    public string? ContentHtml { get; set; } // HTML for assistant
    public string? Sources { get; set; } // JSON array
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Chat Chat { get; set; } = null!;
}