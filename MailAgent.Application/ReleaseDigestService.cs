using System.Text;
using MailAgent.Application.Ollama;

namespace MailAgent.Application;

public sealed class ReleaseDigestService(
  EmailBodyConverter bodyConverter,
  IMailClient mailClient,
  IOllamaClient ollamaClient)
{
  public async Task<ReleaseDigestResult> BuildInboxDigestAsync(
    string folderName,
    int takeCount,
    CancellationToken cancellationToken = default)
  {
    var fetchedMessages = await mailClient.GetLatestFromFolderAsync(folderName, takeCount, cancellationToken);
    var emails = new List<DigestEmail>(capacity: fetchedMessages.Count);

    var emailId = 1;
    foreach (var message in fetchedMessages)
    {
      var bodyPreview = bodyConverter.ConvertToMarkdown(message.HtmlBody, message.TextBody);
      bodyPreview = Truncate(bodyPreview, 1500);

      emails.Add(new DigestEmail(
        emailId++,
        message.Subject,
        message.From,
        message.DateUtc.UtcDateTime,
        bodyPreview));
    }

    var request = new OllamaGenerateRequest("llama3.2:3b", BuildClassifierPrompt(emails), false, 0);
    
    var classifierResult = await ollamaClient.Generate(request, cancellationToken);

    var selected = ParseSelectedIdsOrFallback(classifierResult.Response, emails);

    var digestResult = await ollamaClient.Generate(
      new OllamaGenerateRequest(
        "qwen2.5:7b-instruct",
        BuildDigestPrompt(selected),
        false,
        0),
      cancellationToken);

    return new ReleaseDigestResult(
      TotalFetched: emails.Count,
      Selected: selected.Count,
      Digest: digestResult.Response.Trim());
  }

  private static IReadOnlyList<DigestEmail> ParseSelectedIdsOrFallback(string classifierResponseText, IReadOnlyList<DigestEmail> emails)
  {
    var ids = new HashSet<int>();

    foreach (var part in classifierResponseText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      if (int.TryParse(part, out var id))
      {
        ids.Add(id);
      }
    }

    if (ids.Count == 0 && !string.IsNullOrWhiteSpace(classifierResponseText))
    {
      return emails
        .Where(email => email.Subject.Contains("Вышла версия", StringComparison.OrdinalIgnoreCase)
          || email.Subject.Contains("release", StringComparison.OrdinalIgnoreCase)
          || email.Subject.Contains("version", StringComparison.OrdinalIgnoreCase))
        .ToList();
    }

    return emails.Where(email => ids.Contains(email.Id)).ToList();
  }

  private static string Truncate(string value, int maxLength)
    => value.Length <= maxLength ? value : value[..maxLength];

  private static string BuildDigestPrompt(IReadOnlyList<DigestEmail> selected)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("Составь краткую сводку по релизным письмам.");
    stringBuilder.AppendLine("Формат:");
    stringBuilder.AppendLine("- Название продукта/сервиса — версия");
    stringBuilder.AppendLine("  - 1–2 пункта что важно (если не ясно — так и скажи)");
    stringBuilder.AppendLine("  - source: from + дата");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Сводка должна быть короткой, без воды.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Письма:");

    foreach (var email in selected)
    {
      stringBuilder.AppendLine("---");
      stringBuilder.AppendLine($"Subject: {email.Subject}");
      stringBuilder.AppendLine($"From: {email.From}");
      stringBuilder.AppendLine($"Date: {email.DateUtc:yyyy-MM-dd HH:mm}Z");
      if (!string.IsNullOrWhiteSpace(email.BodyPreview))
      {
        stringBuilder.AppendLine($"Body preview: {email.BodyPreview}");
      }
    }

    return stringBuilder.ToString();
  }

  private static string BuildClassifierPrompt(IReadOnlyList<DigestEmail> emails)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("Ты фильтр входящей почты. Выбери письма, которые относятся к релизам/выходу версий/обновлениям сервисов.");
    stringBuilder.AppendLine("Верни ТОЛЬКО список чисел (Id) через запятую, без текста. Если релизных нет — верни пустую строку.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Письма:");

    foreach (var email in emails)
    {
      stringBuilder.AppendLine($"{email.Id}. [{email.DateUtc:yyyy-MM-dd HH:mm}Z] From: {email.From} | Subject: {email.Subject}");
    }

    return stringBuilder.ToString();
  }
}

public sealed record ReleaseDigestResult(int TotalFetched, int Selected, string Digest);

internal sealed record DigestEmail(int Id, string Subject, string From, DateTime DateUtc, string BodyPreview);
