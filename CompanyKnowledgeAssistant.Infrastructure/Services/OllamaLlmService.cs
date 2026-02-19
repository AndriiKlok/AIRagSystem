using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace CompanyKnowledgeAssistant.Infrastructure.Services;

public class OllamaLlmService
{
    private readonly HttpClient _httpClient;
    private readonly string _ollamaUrl;
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public OllamaLlmService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<string> GenerateResponse(string prompt)
    {
        var request = new
        {
            model = "llama3.1:8b",
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = prompt }
            },
            stream = false,
            options = new { temperature = 0.3, num_predict = 1000 }
        };

        var response = await _httpClient.PostAsJsonAsync($"{_ollamaUrl}/api/chat", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(_jsonOpts);
        return result?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model = "llama3.1:8b",
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = prompt }
            },
            stream = true,
            options = new { temperature = 0.3, num_predict = 1000 }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaUrl}/api/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            ChatResponse? chunk = null;
            try { chunk = JsonSerializer.Deserialize<ChatResponse>(line, _jsonOpts); }
            catch { continue; }

            var content = chunk?.Message?.Content;
            if (!string.IsNullOrEmpty(content))
                yield return content;

            if (chunk?.Done == true) break;
        }
    }

    private string GetSystemPrompt()
    {
        return @"
You are an intelligent knowledge assistant. Your job is to answer questions 
based ONLY on the provided context from company documents.

CRITICAL RULES:
1. Use ONLY information from the context. Do not use external knowledge.
2. If the answer is not in the context, clearly state: 
   'I don't have that information in the available documents.'
3. Format your response using clean, semantic HTML.
4. Cite sources by mentioning document names when relevant.

HTML FORMATTING GUIDELINES:
- Use <p> for paragraphs
- Use <ul> and <li> for unordered lists
- Use <ol> and <li> for ordered lists
- Use <strong> for emphasis
- Use <em> for italics
- Use <code> for technical terms or commands
- Use <h4> for section headers (if needed)
- Use <blockquote> for quotes
- Keep formatting professional and clean

EXAMPLE RESPONSE:
<p>According to the <strong>Employee Handbook</strong>, employees receive 
15 days of paid vacation per year.</p>
<ul>
<li>Must request at least 2 weeks in advance</li>
<li>Unused days carry over (maximum 5 days)</li>
</ul>
";
    }

    private class ChatResponse
    {
        public Message? Message { get; set; }
        public bool Done { get; set; }
    }

    private class Message
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }
}