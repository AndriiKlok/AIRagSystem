using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace CompanyKnowledgeAssistant.Infrastructure.Services;

public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaUrl;

    public EmbeddingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    }

    public async Task<float[]> GenerateEmbedding(string text)
    {
        var request = new
        {
            model = "nomic-embed-text",
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync($"{_ollamaUrl}/api/embeddings", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }

    public async Task<List<float[]>> GenerateEmbeddingsParallel(List<string> texts)
    {
        var tasks = texts.Select(GenerateEmbedding);
        return (await Task.WhenAll(tasks)).ToList();
    }

    private class EmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}