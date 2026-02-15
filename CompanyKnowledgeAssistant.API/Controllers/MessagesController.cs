using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using CompanyKnowledgeAssistant.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyKnowledgeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly EmbeddingService _embeddingService;
    private readonly VectorStoreService _vectorStore;
    private readonly OllamaLlmService _llmService;
    private readonly HtmlSanitizerService _htmlSanitizer;

    public MessagesController(
        AppDbContext dbContext,
        EmbeddingService embeddingService,
        VectorStoreService vectorStore,
        OllamaLlmService llmService,
        HtmlSanitizerService htmlSanitizer)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _llmService = llmService;
        _htmlSanitizer = htmlSanitizer;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Message>>> GetMessages(int chatId)
    {
        var messages = await _dbContext.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var chat = await _dbContext.Chats
            .Include(c => c.Area)
            .FirstOrDefaultAsync(c => c.Id == dto.ChatId);
        if (chat == null)
            return NotFound("Chat not found");

        // Save user message
        var userMessage = new Message
        {
            ChatId = dto.ChatId,
            Role = "user",
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Messages.Add(userMessage);
        await _dbContext.SaveChangesAsync();

        // Generate question embedding
        var questionEmbedding = await _embeddingService.GenerateEmbedding(dto.Content);

        // Search similar chunks
        var similarChunks = await _vectorStore.SearchSimilarChunks(chat.AreaId, questionEmbedding, 7);

        // Build context
        var context = BuildContext(similarChunks);

        // Build prompt
        var prompt = BuildPrompt(dto.Content, context);

        // Generate response
        var rawResponse = await _llmService.GenerateResponse(prompt);

        // Sanitize HTML
        var sanitizedHtml = _htmlSanitizer.Sanitize(rawResponse);

        // Extract plain text
        var plainText = ExtractPlainText(sanitizedHtml);

        // Save assistant message
        var assistantMessage = new Message
        {
            ChatId = dto.ChatId,
            Role = "assistant",
            Content = plainText,
            ContentHtml = sanitizedHtml,
            Sources = System.Text.Json.JsonSerializer.Serialize(similarChunks.Select(c => new
            {
                documentName = c.DocumentName,
                chunkIndex = c.ChunkIndex,
                similarity = c.Similarity
            })),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Messages.Add(assistantMessage);

        // Update chat
        chat.LastMessageAt = DateTime.UtcNow;
        chat.MessageCount++;

        await _dbContext.SaveChangesAsync();

        return Ok(new MessageDto
        {
            Id = assistantMessage.Id,
            Role = "assistant",
            ContentHtml = sanitizedHtml,
            Sources = similarChunks,
            CreatedAt = assistantMessage.CreatedAt
        });
    }

    private string BuildContext(List<ChunkResult> chunks)
    {
        var contextParts = chunks.Select(chunk =>
            $"[Source: {chunk.DocumentName}]\n{chunk.Content}");
        return string.Join("\n\n---\n\n", contextParts);
    }

    private string BuildPrompt(string question, string context)
    {
        return $@"
Context from documents:
{context}

User question: {question}

Provide a detailed, well-formatted HTML response:
";
    }

    private string ExtractPlainText(string html)
    {
        // Simple HTML to text conversion
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "").Trim();
    }
}

public class SendMessageDto
{
    public int ChatId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class MessageDto
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? ContentHtml { get; set; }
    public List<ChunkResult>? Sources { get; set; }
    public DateTime CreatedAt { get; set; }
}