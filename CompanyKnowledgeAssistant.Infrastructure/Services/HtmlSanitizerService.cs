using Ganss.Xss;

namespace CompanyKnowledgeAssistant.Infrastructure.Services;

public class HtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        // Allow only safe tags
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.Add("p");
        _sanitizer.AllowedTags.Add("br");
        _sanitizer.AllowedTags.Add("ul");
        _sanitizer.AllowedTags.Add("ol");
        _sanitizer.AllowedTags.Add("li");
        _sanitizer.AllowedTags.Add("strong");
        _sanitizer.AllowedTags.Add("em");
        _sanitizer.AllowedTags.Add("code");
        _sanitizer.AllowedTags.Add("h4");
        _sanitizer.AllowedTags.Add("blockquote");

        // No attributes allowed (prevents onclick, onerror, etc.)
        _sanitizer.AllowedAttributes.Clear();

        // No CSS allowed
        _sanitizer.AllowedCssProperties.Clear();

        // No schemes (no javascript:, data:, etc.)
        _sanitizer.AllowedSchemes.Clear();
    }

    public string Sanitize(string html)
    {
        return _sanitizer.Sanitize(html);
    }
}