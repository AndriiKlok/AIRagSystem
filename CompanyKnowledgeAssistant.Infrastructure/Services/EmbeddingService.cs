using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace CompanyKnowledgeAssistant.Infrastructure.Services;

public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaUrl;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbedding(string text, int index = -1)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var request = new
        {
            model = "nomic-embed-text",
            prompt = text
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_ollamaUrl}/api/embeddings", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
            var vector = result?.Embedding ?? Array.Empty<float>();

            if (index >= 0)
                _logger.LogDebug("[Embedding] chunk #{Index} — {Elapsed}ms, vector_len={Len}",
                    index, sw.ElapsedMilliseconds, vector.Length);

            return vector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Embedding] chunk #{Index} FAILED after {Elapsed}ms: {Message}",
                index, sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsParallel(List<string> texts)
    {
        _logger.LogInformation("[Embedding] Sending {Count} chunks to Ollama at {Url}", texts.Count, _ollamaUrl);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var tasks = texts.Select((t, i) => GenerateEmbedding(t, i));
        var results = (await Task.WhenAll(tasks)).ToList();

        _logger.LogInformation("[Embedding] All {Count} embeddings done in {Elapsed}ms", results.Count, sw.ElapsedMilliseconds);
        return results;
    }

    private class EmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}