using CompanyKnowledgeAssistant.API.Hubs;
using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using CompanyKnowledgeAssistant.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using ChunkEntity = CompanyKnowledgeAssistant.Core.Entities.Chunk;

namespace CompanyKnowledgeAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly DocumentProcessorService _processor;
    private readonly EmbeddingService _embeddingService;
    private readonly IHubContext<ChatHub> _hubContext;

    public DocumentsController(
        AppDbContext dbContext,
        DocumentProcessorService processor,
        EmbeddingService embeddingService,
        IHubContext<ChatHub> hubContext)
    {
        _dbContext = dbContext;
        _processor = processor;
        _embeddingService = embeddingService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Document>>> GetDocuments(int areaId)
    {
        var documents = await _dbContext.Documents
            .Where(d => d.AreaId == areaId)
            .ToListAsync();
        return Ok(documents);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Document>> GetDocument(int id)
    {
        var document = await _dbContext.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        return Ok(document);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var document = await _dbContext.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        var areaId = document.AreaId;
        var filePath = document.FilePath;

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        var area = await _dbContext.Areas.FindAsync(areaId);
        if (area != null)
        {
            area.DocumentCount = await _dbContext.Documents.CountAsync(d => d.AreaId == areaId);
            await _dbContext.SaveChangesAsync();
        }

        await _hubContext.Clients.Group($"area-{areaId}")
            .SendAsync("DocumentProgress", new { documentId = id, status = "Deleted", progress = 100 });

        return NoContent();
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(int areaId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var allowedExtensions = new[] { ".pdf", ".docx", ".txt", ".md" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Unsupported file type");

        var area = await _dbContext.Areas.FindAsync(areaId);
        if (area == null)
            return NotFound("Area not found");

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var document = new Document
        {
            AreaId = areaId,
            FileName = file.FileName,
            FilePath = filePath,
            FileSize = file.Length,
            ProcessingStatus = "Uploaded"
        };

        _dbContext.Documents.Add(document);

        area.DocumentCount = await _dbContext.Documents.CountAsync(d => d.AreaId == areaId) + 1;
        await _dbContext.SaveChangesAsync();

        // Notify clients that upload is complete
        await _hubContext.Clients.Group($"area-{areaId}")
            .SendAsync("DocumentProgress", new { documentId = document.Id, status = "Uploaded", progress = 100 });

        return Ok(new { document.Id, document.FileName, status = "Uploaded", message = "File uploaded. Click Analyze to process." });
    }

    [HttpPost("{id}/analyze")]
    public async Task<IActionResult> AnalyzeDocument(int id)
    {
        var document = await _dbContext.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        if (document.ProcessingStatus == "Processing")
            return BadRequest(new { message = "Document is already being analyzed." });

        _ = Task.Run(() => ProcessDocumentAsync(id));

        return Ok(new { message = "Analysis started" });
    }

    private async Task ProcessDocumentAsync(int documentId)
    {
        var document = await _dbContext.Documents.FindAsync(documentId);
        if (document == null) return;

        try
        {
            document.ProcessingStatus = "Processing";
            await _dbContext.SaveChangesAsync();

            // Send progress update
            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 10 });

            var fileType = Path.GetExtension(document.FileName).TrimStart('.');
            var text = _processor.ExtractText(document.FilePath, fileType);

            // Send progress update
            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 30 });

            var chunks = _processor.SplitIntoChunks(text);

            // Send progress update
            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 50 });

            // Generate embeddings in parallel
            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsParallel(contents);

            // Send progress update
            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 80 });

            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = embeddings[i];
            }

            // Save chunks
            foreach (var chunk in chunks)
            {
                var chunkEntity = new ChunkEntity
                {
                    DocumentId = documentId,
                    Content = chunk.Content,
                    ChunkIndex = chunk.ChunkIndex,
                    Embedding = VectorStoreService.SerializeVector(chunk.Embedding!)
                };
                _dbContext.Chunks.Add(chunkEntity);
            }

            document.ProcessingStatus = "Completed";
            document.ChunkCount = chunks.Count;
            await _dbContext.SaveChangesAsync();

            // Update area counts
            var area = await _dbContext.Areas.FindAsync(document.AreaId);
            if (area != null)
            {
                area.DocumentCount = await _dbContext.Documents.CountAsync(d => d.AreaId == document.AreaId);
                await _dbContext.SaveChangesAsync();
            }

            // Send completion update
            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Completed", progress = 100 });
        }
        catch (Exception ex)
        {
            document.ProcessingStatus = "Failed";
            document.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync();

            // Send error update
            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Failed", progress = 0, error = ex.Message });
        }
    }
}