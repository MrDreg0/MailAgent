using System.Collections.Concurrent;
using System.Reflection;

namespace MailAgent.Application.Digest;

internal static class DailyDigestPromptTemplateLoader
{
  private const string PromptTemplateNamespace = "MailAgent.Application.Digest.PromptTemplates";
  private static readonly Assembly Assembly = typeof(DailyDigestPromptTemplateLoader).Assembly;
  private static readonly ConcurrentDictionary<string, string> TemplateCache = new();

  internal static string Render(
    string templateName,
    string outputLanguage,
    IReadOnlyDictionary<string, string> placeholders)
  {
    var templateLanguage = ResolveTemplateLanguage(outputLanguage);
    var resourceName = $"{PromptTemplateNamespace}.{templateName}.{templateLanguage}.md";
    var template = TemplateCache.GetOrAdd(resourceName, LoadTemplate);

    return placeholders.Aggregate(
      template,
      (current, placeholder) => current.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value));
  }

  internal static string ResolveTemplateLanguage(string outputLanguage)
  {
    return string.Equals(outputLanguage.Trim(), "Russian", StringComparison.OrdinalIgnoreCase)
      ? "ru"
      : "en";
  }

  private static string LoadTemplate(string resourceName)
  {
    using var stream = Assembly.GetManifestResourceStream(resourceName)
      ?? throw new InvalidOperationException($"Embedded prompt template '{resourceName}' was not found.");
    using var reader = new StreamReader(stream);

    return reader.ReadToEnd();
  }
}
