using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using CompanyKnowledgeAssistant.Infrastructure.Services;
using CompanyKnowledgeAssistant.API.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CompanyKnowledgeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;

    public MessagesController(
        AppDbContext dbContext,
        IHubContext<ChatHub> hubContext,
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
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

        // Save user message immediately
        var userMessage = new Message
        {
            ChatId = dto.ChatId,
            Role = "user",
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Messages.Add(userMessage);
        await _dbContext.SaveChangesAsync();

        // Broadcast user message via SignalR right away
        await _hubContext.Clients.Group($"chat-{dto.ChatId}").SendAsync("ReceiveUserMessage", new
        {
            id = userMessage.Id,
            chatId = userMessage.ChatId,
            role = userMessage.Role,
            content = userMessage.Content,
            createdAt = userMessage.CreatedAt
        });

        // Fire background streaming task
        var chatId = dto.ChatId;
        var areaId = chat.AreaId;
        var content = dto.Content;
        _ = Task.Run(() => StreamAssistantResponseAsync(chatId, areaId, content));

        return Ok(new { userMessageId = userMessage.Id });
    }

    private async Task StreamAssistantResponseAsync(int chatId, int areaId, string content)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<VectorStoreService>();
        var llmService = scope.ServiceProvider.GetRequiredService<OllamaLlmService>();
        var htmlSanitizer = scope.ServiceProvider.GetRequiredService<HtmlSanitizerService>();

        try
        {
            // Signal that bot is "thinking" (retrieving context)
            await _hubContext.Clients.Group($"chat-{chatId}").SendAsync("BotTyping", chatId);

            var questionEmbedding = await embeddingService.GenerateEmbedding(content);
            var similarChunks = await vectorStore.SearchSimilarChunks(areaId, questionEmbedding, 7);
            var context = BuildContext(similarChunks);
            var prompt = BuildPrompt(content, context);

            var fullHtml = new StringBuilder();

            await foreach (var token in llmService.GenerateResponseStreamAsync(prompt))
            {
                fullHtml.Append(token);

                // Strip HTML tags so the streaming text is readable plain text
                var plainToken = System.Text.RegularExpressions.Regex.Replace(token, "<[^>]*>", "");
                plainToken = System.Text.RegularExpressions.Regex.Replace(plainToken, "[<>]", "");

                if (!string.IsNullOrEmpty(plainToken))
                {
                    await _hubContext.Clients.Group($"chat-{chatId}")
                        .SendAsync("ReceiveMessageChunk", chatId, plainToken);
                }
            }

            // Sanitize and save assistant message
            var sanitizedHtml = htmlSanitizer.Sanitize(fullHtml.ToString());
            var plainText = ExtractPlainText(sanitizedHtml);
            var sourcesJson = System.Text.Json.JsonSerializer.Serialize(similarChunks.Select(c => new
            {
                documentName = c.DocumentName,
                chunkIndex = c.ChunkIndex,
                similarity = c.Similarity
            }));

            var assistantMessage = new Message
            {
                ChatId = chatId,
                Role = "assistant",
                Content = plainText,
                ContentHtml = sanitizedHtml,
                Sources = sourcesJson,
                CreatedAt = DateTime.UtcNow
            };
            db.Messages.Add(assistantMessage);

            var chat = await db.Chats.FindAsync(chatId);
            if (chat != null)
            {
                chat.LastMessageAt = DateTime.UtcNow;
                chat.MessageCount++;
            }
            await db.SaveChangesAsync();

            // Signal completion with full formatted message
            await _hubContext.Clients.Group($"chat-{chatId}").SendAsync("MessageStreamComplete", new
            {
                id = assistantMessage.Id,
                chatId = chatId,
                role = "assistant",
                content = plainText,
                contentHtml = sanitizedHtml,
                sources = sourcesJson,
                createdAt = assistantMessage.CreatedAt
            });
        }
        catch (Exception ex)
        {
            await _hubContext.Clients.Group($"chat-{chatId}")
                .SendAsync("MessageStreamError", chatId, ex.Message);
        }
    }

    private static string BuildContext(List<ChunkResult> chunks)
    {
        var parts = chunks.Select(c => $"[Source: {c.DocumentName}]\n{c.Content}");
        return string.Join("\n\n---\n\n", parts);
    }

    private static string BuildPrompt(string question, string context)
    {
        return $@"Context from documents:
{context}

User question: {question}

Provide a detailed, well-formatted HTML response:";
    }

    private static string ExtractPlainText(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "").Trim();
}

public class SendMessageDto
{
    public int ChatId { get; set; }
    public string Content { get; set; } = string.Empty;
}
