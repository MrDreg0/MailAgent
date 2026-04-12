using System.Text;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;

namespace MailAgent.Application.Digest;

public sealed class DailyDigestService(
  IMailRepository mailRepository,
  ILlmClient llmClient,
  LlmSettings llmSettings,
  ILogger<DailyDigestService> logger)
{
  private const int ClassifierBatchSize = 50;
  private const int DigestBatchSize = 5;

  public async Task<DailyDigestBuildResult> BuildForDate(
    string folderName,
    DateOnly digestDate,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    var fromUtc = new DateTimeOffset(digestDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    var toUtc = new DateTimeOffset(digestDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    var storedMails = await mailRepository.GetByUtcRangeFromFolder(
      folderName,
      fromUtc,
      toUtc,
      cancellationToken);

    var emails = new List<DailyDigestEmail>(capacity: storedMails.Count);
    var candidateId = 1;

    emails.AddRange(
      from message in storedMails
      let bodyPreview = Truncate(message.MarkdownBody, 1500)
      select new DailyDigestEmail(
        CandidateId: candidateId++,
        Subject: message.Subject,
        From: message.From,
        DateUtc: message.DateUtc.UtcDateTime,
        BodyPreview: bodyPreview));

    var selected = await SelectReleaseEmails(emails, cancellationToken);
    var digestMarkdown = selected.Count == 0
      ? BuildEmptyDigest(digestDate)
      : await BuildDigest(digestDate, selected, cancellationToken);

    return new DailyDigestBuildResult(
      Folder: folderName,
      DigestDate: digestDate,
      TotalFetched: emails.Count,
      Selected: selected.Count,
      DigestMarkdown: digestMarkdown.Trim());
  }

  private async Task<string> BuildDigest(
    DateOnly digestDate,
    IReadOnlyList<DailyDigestEmail> selected,
    CancellationToken cancellationToken)
  {
    if (selected.Count <= DigestBatchSize)
    {
      return await GenerateDigest(digestDate, BuildDigestPrompt(digestDate, selected), selected.Count, cancellationToken);
    }

    var partialDigests = new List<string>();
    var batchIndex = 0;

    foreach (var batch in selected.Chunk(DigestBatchSize))
    {
      batchIndex++;

      logger.LogInformation(
        "Running partial daily digest generation. DigestDate={DigestDate}, Batch={BatchIndex}, SelectedEmails={SelectedCount}.",
        digestDate,
        batchIndex,
        batch.Length);

      partialDigests.Add(await GenerateDigest(digestDate, BuildDigestPrompt(digestDate, batch), batch.Length, cancellationToken));
    }

    var mergePrompt = BuildDigestMergePrompt(digestDate, partialDigests);

    logger.LogInformation(
      "Running final daily digest merge with model {Model}. DigestDate={DigestDate}, PartialDigests={PartialDigestCount}, PromptLength={PromptLength}.",
      llmSettings.MainModel,
      digestDate,
      partialDigests.Count,
      mergePrompt.Length);

    var digestResult = await llmClient.Generate(
      new LlmGenerateRequest(llmSettings.MainModel, mergePrompt),
      cancellationToken);

    return digestResult.Response;
  }

  private async Task<string> GenerateDigest(
    DateOnly digestDate,
    string digestPrompt,
    int selectedCount,
    CancellationToken cancellationToken)
  {
    logger.LogInformation(
      "Running daily digest generation with model {Model}. DigestDate={DigestDate}, SelectedEmails={SelectedCount}, PromptLength={PromptLength}.",
      llmSettings.MainModel,
      digestDate,
      selectedCount,
      digestPrompt.Length);

    var digestResult = await llmClient.Generate(
      new LlmGenerateRequest(llmSettings.MainModel, digestPrompt),
      cancellationToken);

    return digestResult.Response;
  }

  private async Task<IReadOnlyList<DailyDigestEmail>> SelectReleaseEmails(
    IReadOnlyList<DailyDigestEmail> emails,
    CancellationToken cancellationToken)
  {
    if (emails.Count == 0)
    {
      return [];
    }

    var selected = new List<DailyDigestEmail>();
    var batchIndex = 0;

    foreach (var batch in emails.Chunk(ClassifierBatchSize))
    {
      batchIndex++;
      var classifierPrompt = BuildClassifierPrompt(batch);

      logger.LogInformation(
        "Running daily digest release classification with model {Model}. Batch={BatchIndex}, Emails={EmailCount}, PromptLength={PromptLength}.",
        llmSettings.FastModel,
        batchIndex,
        batch.Length,
        classifierPrompt.Length);

      var classifierResult = await llmClient.Generate(
        new LlmGenerateRequest(llmSettings.FastModel, classifierPrompt),
        cancellationToken);

      selected.AddRange(ParseSelectedIdsOrFallback(classifierResult.Response, batch));
    }

    return selected;
  }

  private static IReadOnlyList<DailyDigestEmail> ParseSelectedIdsOrFallback(
    string classifierResponseText,
    IReadOnlyList<DailyDigestEmail> emails)
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
          || email.Subject.Contains("version", StringComparison.OrdinalIgnoreCase)
          || email.Subject.Contains("обновлен", StringComparison.OrdinalIgnoreCase))
        .ToList();
    }

    return emails.Where(email => ids.Contains(email.CandidateId)).ToList();
  }

  private static string BuildEmptyDigest(DateOnly digestDate)
  {
    return $"""
      # Release Digest for {digestDate:yyyy-MM-dd}

      ## Highlights
      - No release mails were selected for this day.

      ## Releases
      - No release entries.
      """;
  }

  private static string Truncate(string value, int maxLength)
    => value.Length <= maxLength ? value : value[..maxLength];

  private static string BuildDigestPrompt(DateOnly digestDate, IReadOnlyList<DailyDigestEmail> selected)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"Составь ежедневный markdown-дайджест релизов за {digestDate:yyyy-MM-dd}.");
    stringBuilder.AppendLine("Верни только markdown, без пояснений вне структуры.");
    stringBuilder.AppendLine("Структура ответа строго такая:");
    stringBuilder.AppendLine($"# Release Digest for {digestDate:yyyy-MM-dd}");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("## Highlights");
    stringBuilder.AppendLine("- 2-4 коротких пункта с самым важным за день.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("## Releases");
    stringBuilder.AppendLine("### Product or Service - Version");
    stringBuilder.AppendLine("- 1-3 конкретных важных изменения");
    stringBuilder.AppendLine("- source: from + date");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Не выдумывай версии или детали, которых нет в письмах. Если деталей мало, так и напиши кратко.");
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

  private static string BuildDigestMergePrompt(DateOnly digestDate, IReadOnlyList<string> partialDigests)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"Объедини частичные markdown-дайджесты релизов за {digestDate:yyyy-MM-dd} в один итоговый markdown-документ.");
    stringBuilder.AppendLine("Убери дубли, сохрани строгую структуру:");
    stringBuilder.AppendLine($"# Release Digest for {digestDate:yyyy-MM-dd}");
    stringBuilder.AppendLine("## Highlights");
    stringBuilder.AppendLine("## Releases");
    stringBuilder.AppendLine("### Product or Service - Version");
    stringBuilder.AppendLine("- ...");
    stringBuilder.AppendLine("- source: from + date");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Частичные дайджесты:");

    foreach (var partialDigest in partialDigests)
    {
      stringBuilder.AppendLine("---");
      stringBuilder.AppendLine(partialDigest);
    }

    return stringBuilder.ToString();
  }

  private static string BuildClassifierPrompt(IReadOnlyList<DailyDigestEmail> emails)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("Ты фильтр входящей почты. Выбери письма, которые относятся к релизам, выходу версий или заметным обновлениям сервисов.");
    stringBuilder.AppendLine("Верни ТОЛЬКО список чисел (Id) через запятую, без текста. Если релизных нет - верни пустую строку.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Письма:");

    foreach (var email in emails)
    {
      stringBuilder.AppendLine($"{email.CandidateId}. [{email.DateUtc:yyyy-MM-dd HH:mm}Z] From: {email.From} | Subject: {email.Subject}");
    }

    return stringBuilder.ToString();
  }

  private sealed record DailyDigestEmail(
    int CandidateId,
    string Subject,
    string From,
    DateTime DateUtc,
    string BodyPreview);
}
