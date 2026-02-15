using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyKnowledgeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ChatsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Chat>>> GetChats(int areaId)
    {
        var chats = await _dbContext.Chats
            .Where(c => c.AreaId == areaId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
        return Ok(chats);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Chat>> GetChat(int id)
    {
        var chat = await _dbContext.Chats.FindAsync(id);
        if (chat == null)
            return NotFound();

        return Ok(chat);
    }

    [HttpPost]
    public async Task<ActionResult<Chat>> CreateChat([FromBody] ChatCreateDto dto)
    {
        var area = await _dbContext.Areas.FindAsync(dto.AreaId);
        if (area == null)
            return NotFound("Area not found");

        var chat = new Chat
        {
            AreaId = dto.AreaId,
            Name = dto.Name ?? "New Chat",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Chats.Add(chat);
        await _dbContext.SaveChangesAsync();

        // Update area chat count
        area.ChatCount = await _dbContext.Chats.CountAsync(c => c.AreaId == dto.AreaId);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetChat), new { id = chat.Id }, chat);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateChat(int id, [FromBody] ChatUpdateDto dto)
    {
        var chat = await _dbContext.Chats.FindAsync(id);
        if (chat == null)
            return NotFound();

        chat.Name = dto.Name;
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteChat(int id)
    {
        var chat = await _dbContext.Chats.FindAsync(id);
        if (chat == null)
            return NotFound();

        _dbContext.Chats.Remove(chat);
        await _dbContext.SaveChangesAsync();

        // Update area chat count
        var area = await _dbContext.Areas.FindAsync(chat.AreaId);
        if (area != null)
        {
            area.ChatCount = await _dbContext.Chats.CountAsync(c => c.AreaId == chat.AreaId);
            await _dbContext.SaveChangesAsync();
        }

        return NoContent();
    }
}

public class ChatCreateDto
{
    public int AreaId { get; set; }
    public string? Name { get; set; }
}

public class ChatUpdateDto
{
    public string Name { get; set; } = string.Empty;
}