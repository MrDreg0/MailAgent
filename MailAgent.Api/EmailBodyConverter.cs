using HtmlAgilityPack;
using ReverseMarkdown;

namespace MailAgent;

public sealed class EmailBodyConverter
{
  private static readonly HashSet<string> RemovableNodes = new(StringComparer.OrdinalIgnoreCase)
  {
    "head",
    "meta",
    "style",
    "script",
    "title",
    "o:p"
  };

  private readonly Converter _converter = new(new Config
  {
    UnknownTags = Config.UnknownTagsOption.Drop,
    GithubFlavored = true,
    RemoveComments = true,
    SmartHrefHandling = true
  });

  public string ConvertToMarkdown(string? htmlBody, string? textBody)
  {
    if (string.IsNullOrWhiteSpace(htmlBody))
    {
      return !string.IsNullOrWhiteSpace(textBody)
        ? Normalize(textBody)
        : string.Empty;
    }

    var cleanedHtml = CleanHtml(htmlBody);
    var markdown = _converter.Convert(cleanedHtml);
    
    return Normalize(markdown);
  }

  private static string CleanHtml(string html)
  {
    var document = new HtmlDocument();
    document.LoadHtml(html);

    RemoveJunkNodes(document);
    RemoveJunkAttributes(document);

    return document.DocumentNode.OuterHtml;
  }

  private static void RemoveJunkNodes(HtmlDocument document)
  {
    var nodesToRemove = document.DocumentNode
      .Descendants()
      .Where(node => RemovableNodes.Contains(node.Name))
      .ToList();

    foreach (var node in nodesToRemove)
    {
      node.Remove();
    }
  }

  private static void RemoveJunkAttributes(HtmlDocument document)
  {
    foreach (var node in document.DocumentNode.Descendants())
    {
      if (!node.HasAttributes)
      {
        continue;
      }

      if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
      {
        var href = node.GetAttributeValue("href", string.Empty);
        node.Attributes.RemoveAll();
        if (!string.IsNullOrWhiteSpace(href))
        {
          node.SetAttributeValue("href", href);
        }
        continue;
      }

      if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
      {
        var src = node.GetAttributeValue("src", string.Empty);
        var alt = node.GetAttributeValue("alt", string.Empty);
        node.Attributes.RemoveAll();
        if (!string.IsNullOrWhiteSpace(src))
        {
          node.SetAttributeValue("src", src);
        }
        if (!string.IsNullOrWhiteSpace(alt))
        {
          node.SetAttributeValue("alt", alt);
        }
        continue;
      }

      node.Attributes.RemoveAll();
    }
  }

  private static string Normalize(string text)
  {
    var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
    normalized = HtmlEntity.DeEntitize(normalized);

    while (normalized.Contains("\n\n\n", StringComparison.Ordinal))
    {
      normalized = normalized.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
    }

    return normalized.Trim();
  }
}
