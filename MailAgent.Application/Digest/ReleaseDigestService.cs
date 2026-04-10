using System.Text;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;
namespace MailAgent.Application.Digest;

public sealed class ReleaseDigestService(
  IMailRepository mailRepository,
  ILlmClient llmClient,
  LlmSettings llmSettings,
  ILogger<ReleaseDigestService> logger)
{
  private const int ClassifierBatchSize = 50;
  private const int DigestBatchSize = 5;

  public async Task<ReleaseDigestResult> BuildInboxDigestAsync(
    string folderName,
    TimeSpan period,
    CancellationToken cancellationToken = default)
  {
    var storedMails = await mailRepository.GetByPeriodFromFolder(folderName, period, cancellationToken);
    var emails = new List<DigestEmail>(capacity: storedMails.Count);

    var emailId = 1;
    
    emails.AddRange(
      from message in storedMails 
      let bodyPreview = Truncate(message.MarkdownBody, 1500) 
      select new DigestEmail(emailId++, message.Subject, message.From, message.DateUtc.UtcDateTime, bodyPreview));

    var selected = await SelectReleaseEmails(emails, cancellationToken);
    var digestText = await BuildDigest(selected, cancellationToken);

    return new ReleaseDigestResult(
      TotalFetched: emails.Count,
      Selected: selected.Count,
      Digest: digestText.Trim());
  }

  private async Task<string> BuildDigest(
    IReadOnlyList<DigestEmail> selected,
    CancellationToken cancellationToken)
  {
    if (selected.Count <= DigestBatchSize)
    {
      return await GenerateDigest(BuildDigestPrompt(selected), selected.Count, cancellationToken);
    }

    var partialDigests = new List<string>();
    var batchIndex = 0;

    foreach (var batch in selected.Chunk(DigestBatchSize))
    {
      batchIndex++;

      logger.LogInformation(
        "Running partial release digest generation. Batch: {BatchIndex}. Selected emails: {SelectedCount}.",
        batchIndex,
        batch.Length);

      partialDigests.Add(await GenerateDigest(BuildDigestPrompt(batch), batch.Length, cancellationToken));
    }

    var mergePrompt = BuildDigestMergePrompt(partialDigests);

    logger.LogInformation(
      "Running final release digest merge with LLM model {Model}. Partial digests: {PartialDigestCount}. Prompt length: {PromptLength}.",
      llmSettings.MainModel,
      partialDigests.Count,
      mergePrompt.Length);

    var digestResult = await llmClient.Generate(
      new LlmGenerateRequest(llmSettings.MainModel, mergePrompt),
      cancellationToken);

    return digestResult.Response;
  }

  private async Task<string> GenerateDigest(
    string digestPrompt,
    int selectedCount,
    CancellationToken cancellationToken)
  {
    logger.LogInformation(
      "Running release digest generation with LLM model {Model}. Selected emails: {SelectedCount}. Prompt length: {PromptLength}.",
      llmSettings.MainModel,
      selectedCount,
      digestPrompt.Length);

    var digestResult = await llmClient.Generate(
      new LlmGenerateRequest(
        llmSettings.MainModel,
        digestPrompt),
      cancellationToken);

    return digestResult.Response;
  }

  private async Task<IReadOnlyList<DigestEmail>> SelectReleaseEmails(
    IReadOnlyList<DigestEmail> emails,
    CancellationToken cancellationToken)
  {
    var selected = new List<DigestEmail>();
    var batchIndex = 0;

    foreach (var batch in emails.Chunk(ClassifierBatchSize))
    {
      batchIndex++;
      var classifierPrompt = BuildClassifierPrompt(batch);

      logger.LogInformation(
        "Running release mail classification with LLM model {Model}. Batch: {BatchIndex}. Emails: {EmailCount}. Prompt length: {PromptLength}.",
        llmSettings.FastModel,
        batchIndex,
        batch.Length,
        classifierPrompt.Length);

      var request = new LlmGenerateRequest(llmSettings.FastModel, classifierPrompt);
      var classifierResult = await llmClient.Generate(request, cancellationToken);

      selected.AddRange(ParseSelectedIdsOrFallback(classifierResult.Response, batch));
    }

    return selected;
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

  private static string BuildDigestMergePrompt(IReadOnlyList<string> partialDigests)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("Объедини частичные сводки релизных писем в одну краткую итоговую сводку.");
    stringBuilder.AppendLine("Убери дубли, сохрани формат:");
    stringBuilder.AppendLine("- Название продукта/сервиса — версия");
    stringBuilder.AppendLine("  - 1–2 пункта что важно (если не ясно — так и скажи)");
    stringBuilder.AppendLine("  - source: from + дата");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Частичные сводки:");

    foreach (var partialDigest in partialDigests)
    {
      stringBuilder.AppendLine("---");
      stringBuilder.AppendLine(partialDigest);
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
