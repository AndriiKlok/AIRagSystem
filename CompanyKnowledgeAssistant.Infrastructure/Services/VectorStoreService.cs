using CompanyKnowledgeAssistant.Core.Entities;
using CompanyKnowledgeAssistant.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyKnowledgeAssistant.Infrastructure.Services;

public class VectorStoreService
{
    private readonly AppDbContext _dbContext;

    public VectorStoreService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        var dotProduct = 0.0f;
        var magnitudeA = 0.0f;
        var magnitudeB = 0.0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    public async Task<List<ChunkResult>> SearchSimilarChunks(int areaId, float[] questionEmbedding, int topK = 7)
    {
        var chunks = await _dbContext.Chunks
            .Include(c => c.Document)
            .Where(c => c.Document.AreaId == areaId && c.Document.ProcessingStatus == "Completed")
            .ToListAsync();

        var results = new List<ChunkResult>();

        foreach (var chunk in chunks)
        {
            var chunkEmbedding = DeserializeVector(chunk.Embedding);
            var similarity = CosineSimilarity(questionEmbedding, chunkEmbedding);

            results.Add(new ChunkResult
            {
                ChunkId = chunk.Id,
                Content = chunk.Content,
                DocumentName = chunk.Document.FileName,
                ChunkIndex = chunk.ChunkIndex,
                Similarity = similarity
            });
        }

        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    public static byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] DeserializeVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}

public class ChunkResult
{
    public int ChunkId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public float Similarity { get; set; }
}