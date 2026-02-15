using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using CompanyKnowledgeAssistant.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
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

    public DocumentsController(
        AppDbContext dbContext,
        DocumentProcessorService processor,
        EmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _processor = processor;
        _embeddingService = embeddingService;
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
            ProcessingStatus = "Uploading"
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        // Start processing in background
        _ = Task.Run(() => ProcessDocumentAsync(document.Id));

        return Ok(new { document.Id, message = "Upload started, processing in background" });
    }

    private async Task ProcessDocumentAsync(int documentId)
    {
        var document = await _dbContext.Documents.FindAsync(documentId);
        if (document == null) return;

        try
        {
            document.ProcessingStatus = "Processing";
            await _dbContext.SaveChangesAsync();

            var fileType = Path.GetExtension(document.FileName).TrimStart('.');
            var text = _processor.ExtractText(document.FilePath, fileType);
            var chunks = _processor.SplitIntoChunks(text);

            // Generate embeddings in parallel
            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsParallel(contents);

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
        }
        catch (Exception ex)
        {
            document.ProcessingStatus = "Failed";
            document.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync();
        }
    }
}