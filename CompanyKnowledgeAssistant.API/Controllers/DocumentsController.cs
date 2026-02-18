using CompanyKnowledgeAssistant.API.Hubs;
using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using CompanyKnowledgeAssistant.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly ILogger<DocumentsController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DocumentsController(
        AppDbContext dbContext,
        DocumentProcessorService processor,
        EmbeddingService embeddingService,
        IHubContext<ChatHub> hubContext,
        ILogger<DocumentsController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _processor = processor;
        _embeddingService = embeddingService;
        _hubContext = hubContext;
        _logger = logger;
        _scopeFactory = scopeFactory;
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
        // Create a dedicated DI scope so we get a fresh AppDbContext.
        // The controller's injected context is already disposed by the time
        // this background task runs (the HTTP request lifetime ended).
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();

        var document = await db.Documents.FindAsync(documentId);
        if (document == null)
        {
            _logger.LogWarning("[Analyze] Document {Id} not found — aborting.", documentId);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("[Analyze] START doc={Id} file='{File}'", documentId, document.FileName);

        try
        {
            document.ProcessingStatus = "Processing";
            await db.SaveChangesAsync();

            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 10 });

            _logger.LogInformation("[Analyze] [{Elapsed}ms] Extracting text from '{File}' (type={Type})",
                sw.ElapsedMilliseconds, document.FileName,
                Path.GetExtension(document.FileName).TrimStart('.'));

            var fileType = Path.GetExtension(document.FileName).TrimStart('.');
            var text = _processor.ExtractText(document.FilePath, fileType);
            _logger.LogInformation("[Analyze] [{Elapsed}ms] Extracted {Chars} chars", sw.ElapsedMilliseconds, text.Length);

            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 30 });

            var chunks = _processor.SplitIntoChunks(text);
            _logger.LogInformation("[Analyze] [{Elapsed}ms] Split into {Count} chunks", sw.ElapsedMilliseconds, chunks.Count);

            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 50 });

            _logger.LogInformation("[Analyze] [{Elapsed}ms] Starting embedding generation for {Count} chunks via Ollama...",
                sw.ElapsedMilliseconds, chunks.Count);

            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = await embeddingService.GenerateEmbeddingsParallel(contents);

            _logger.LogInformation("[Analyze] [{Elapsed}ms] Embeddings done — got {Count} vectors",
                sw.ElapsedMilliseconds, embeddings.Count);

            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Processing", progress = 80 });

            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = embeddings[i];
            }

            _logger.LogInformation("[Analyze] [{Elapsed}ms] Saving {Count} chunks to DB...", sw.ElapsedMilliseconds, chunks.Count);

            foreach (var chunk in chunks)
            {
                var chunkEntity = new ChunkEntity
                {
                    DocumentId = documentId,
                    Content = chunk.Content,
                    ChunkIndex = chunk.ChunkIndex,
                    Embedding = VectorStoreService.SerializeVector(chunk.Embedding!)
                };
                db.Chunks.Add(chunkEntity);
            }

            document.ProcessingStatus = "Completed";
            document.ChunkCount = chunks.Count;
            await db.SaveChangesAsync();

            var area = await db.Areas.FindAsync(document.AreaId);
            if (area != null)
            {
                area.DocumentCount = await db.Documents.CountAsync(d => d.AreaId == document.AreaId);
                await db.SaveChangesAsync();
            }

            _logger.LogInformation("[Analyze] [{Elapsed}ms] COMPLETED doc={Id} chunks={Count}",
                sw.ElapsedMilliseconds, documentId, chunks.Count);

            await _hubContext.Clients.Group($"area-{document.AreaId}")
                .SendAsync("DocumentProgress", new { documentId, status = "Completed", progress = 100 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Analyze] [{Elapsed}ms] FAILED doc={Id}: {Message}",
                sw.ElapsedMilliseconds, documentId, ex.Message);

            // Re-fetch document in case the tracked entity is in a bad state
            var failedDoc = await db.Documents.FindAsync(documentId);
            if (failedDoc != null)
            {
                failedDoc.ProcessingStatus = "Failed";
                failedDoc.ErrorMessage = ex.Message;
                await db.SaveChangesAsync();
            }

            // Try to get areaId for SignalR notification
            int areaId = failedDoc?.AreaId ?? 0;
            if (areaId > 0)
            {
                await _hubContext.Clients.Group($"area-{areaId}")
                    .SendAsync("DocumentProgress", new { documentId, status = "Failed", progress = 0, error = ex.Message });
            }
        }
    }
}