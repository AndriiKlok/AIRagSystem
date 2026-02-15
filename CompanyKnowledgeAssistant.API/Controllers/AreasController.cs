using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyKnowledgeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AreasController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AreasController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Area>>> GetAreas()
    {
        var areas = await _dbContext.Areas.ToListAsync();
        return Ok(areas);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Area>> GetArea(int id)
    {
        var area = await _dbContext.Areas.FindAsync(id);
        if (area == null)
            return NotFound();

        return Ok(area);
    }

    [HttpPost]
    public async Task<ActionResult<Area>> CreateArea([FromBody] AreaCreateDto dto)
    {
        var area = new Area
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Areas.Add(area);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetArea), new { id = area.Id }, area);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateArea(int id, [FromBody] AreaUpdateDto dto)
    {
        var area = await _dbContext.Areas.FindAsync(id);
        if (area == null)
            return NotFound();

        area.Name = dto.Name;
        area.Description = dto.Description;
        area.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteArea(int id)
    {
        var area = await _dbContext.Areas.FindAsync(id);
        if (area == null)
            return NotFound();

        _dbContext.Areas.Remove(area);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
}

public class AreaCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AreaUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}