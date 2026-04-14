using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;

namespace MailAgent.Application.Digest;

public sealed class DailyDigestService(
  IMailRepository mailRepository,
  ILlmClient llmClient,
  LlmSettings llmSettings,
  DailyDigestSettings dailyDigestSettings,
  ILogger<DailyDigestService> logger)
{
  private const int ClassifierBatchSize = 50;
  private const int DigestBatchSize = 5;
  private const int DigestInputMaxLength = 1500;
  private const int NormalizationInputMaxLength = 6000;

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
      let digestInput = BuildInput(message.MarkdownBody, DigestInputMaxLength)
      let normalizationInput = BuildInput(message.MarkdownBody, NormalizationInputMaxLength)
      select new DailyDigestEmail(
        CandidateId: candidateId++,
        Subject: message.Subject,
        From: message.From,
        DateUtc: message.DateUtc.UtcDateTime,
        DigestInput: digestInput,
        NormalizationInput: normalizationInput));

    var selected = await SelectReleaseEmails(emails, cancellationToken);
    var digestMarkdown = selected.Count == 0
      ? await BuildEmptyDigest(digestDate, cancellationToken)
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
    var normalized = await NormalizeSelectedEmails(selected, digestDate, cancellationToken);

    if (normalized.Count <= DigestBatchSize)
    {
      return await GenerateDigest(digestDate, BuildDigestPrompt(digestDate, normalized), normalized.Count, cancellationToken);
    }

    var partialDigests = new List<string>();
    var batchIndex = 0;

    foreach (var batch in normalized.Chunk(DigestBatchSize))
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

  private async Task<IReadOnlyList<DailyDigestEmail>> NormalizeSelectedEmails(
    IReadOnlyList<DailyDigestEmail> selected,
    DateOnly digestDate,
    CancellationToken cancellationToken)
  {
    var normalized = new List<DailyDigestEmail>(selected.Count);

    foreach (var email in selected)
    {
      var prompt = BuildNormalizationPrompt(email);

      logger.LogInformation(
        "Running daily digest normalization with model {Model}. DigestDate={DigestDate}, CandidateId={CandidateId}, PromptLength={PromptLength}.",
        llmSettings.FastModel,
        digestDate,
        email.CandidateId,
        prompt.Length);

      var response = await llmClient.Generate(
        new LlmGenerateRequest(llmSettings.FastModel, prompt),
        cancellationToken);

      var normalizedSummary = ParseNormalizedSummaryOrFallback(response.Response, email.DigestInput);

      normalized.Add(email with { DigestInput = normalizedSummary });
    }

    return normalized;
  }

  private async Task<string> BuildEmptyDigest(
    DateOnly digestDate,
    CancellationToken cancellationToken)
  {
    var emptyDigestPrompt = BuildEmptyDigestPrompt(digestDate);

    logger.LogInformation(
      "Running empty daily digest generation with model {Model}. DigestDate={DigestDate}, PromptLength={PromptLength}.",
      llmSettings.FastModel,
      digestDate,
      emptyDigestPrompt.Length);

    var digestResult = await llmClient.Generate(
      new LlmGenerateRequest(llmSettings.FastModel, emptyDigestPrompt),
      cancellationToken);

    return digestResult.Response;
  }

  private static string Truncate(string value, int maxLength)
    => value.Length <= maxLength ? value : value[..maxLength];

  private static string BuildInput(string markdownBody, int maxLength)
  {
    if (string.IsNullOrWhiteSpace(markdownBody))
    {
      return string.Empty;
    }

    return Truncate(markdownBody.Replace("\r\n", "\n").Trim(), maxLength).Trim();
  }

  private static string ParseNormalizedSummaryOrFallback(
    string normalizedResponse,
    string fallbackDigestInput)
  {
    var normalized = normalizedResponse
      .Replace("\r\n", "\n")
      .Trim();

    return string.IsNullOrWhiteSpace(normalized)
      ? fallbackDigestInput
      : normalized;
  }

  private string BuildEmptyDigestPrompt(DateOnly digestDate)
  {
    return DailyDigestPromptTemplateLoader.Render(
      templateName: "DailyDigest.Empty",
      outputLanguage: dailyDigestSettings.OutputLanguage,
      placeholders: new Dictionary<string, string>
      {
        ["DIGEST_DATE"] = digestDate.ToString("yyyy-MM-dd")
      });
  }

  private string BuildDigestPrompt(DateOnly digestDate, IReadOnlyList<DailyDigestEmail> selected)
  {
    return DailyDigestPromptTemplateLoader.Render(
      templateName: "DailyDigest.Main",
      outputLanguage: dailyDigestSettings.OutputLanguage,
      placeholders: new Dictionary<string, string>
      {
        ["DIGEST_DATE"] = digestDate.ToString("yyyy-MM-dd"),
        ["EMAILS"] = BuildEmailsBlock(selected)
      });
  }

  private string BuildNormalizationPrompt(DailyDigestEmail email)
  {
    return DailyDigestPromptTemplateLoader.Render(
      templateName: "DailyDigest.Normalize",
      outputLanguage: dailyDigestSettings.OutputLanguage,
      placeholders: new Dictionary<string, string>
      {
        ["SUBJECT"] = email.Subject,
        ["FROM"] = email.From,
        ["DATE_UTC"] = email.DateUtc.ToString("yyyy-MM-dd HH:mm"),
        ["BODY_PREVIEW"] = email.NormalizationInput
      });
  }

  private string BuildDigestMergePrompt(DateOnly digestDate, IReadOnlyList<string> partialDigests)
  {
    return DailyDigestPromptTemplateLoader.Render(
      templateName: "DailyDigest.Merge",
      outputLanguage: dailyDigestSettings.OutputLanguage,
      placeholders: new Dictionary<string, string>
      {
        ["DIGEST_DATE"] = digestDate.ToString("yyyy-MM-dd"),
        ["PARTIAL_DIGESTS"] = BuildPartialDigestsBlock(partialDigests)
      });
  }

  private string BuildClassifierPrompt(IReadOnlyList<DailyDigestEmail> emails)
  {
    return DailyDigestPromptTemplateLoader.Render(
      templateName: "DailyDigest.Classifier",
      outputLanguage: dailyDigestSettings.OutputLanguage,
      placeholders: new Dictionary<string, string>
      {
        ["EMAILS"] = BuildClassifierEmailsBlock(emails)
      });
  }

  private static string BuildEmailsBlock(IReadOnlyList<DailyDigestEmail> emails)
  {
    var lines = new List<string>();

    foreach (var email in emails)
    {
      lines.Add("---");
      lines.Add($"Subject: {email.Subject}");
      lines.Add($"From: {email.From}");
      lines.Add($"Date: {email.DateUtc:yyyy-MM-dd HH:mm}Z");
      if (!string.IsNullOrWhiteSpace(email.DigestInput))
      {
        lines.Add($"Normalized summary: {email.DigestInput}");
      }
    }

    return string.Join('\n', lines);
  }

  private static string BuildPartialDigestsBlock(IReadOnlyList<string> partialDigests)
  {
    var lines = new List<string>();

    foreach (var partialDigest in partialDigests)
    {
      lines.Add("---");
      lines.Add(partialDigest);
    }

    return string.Join('\n', lines);
  }

  private static string BuildClassifierEmailsBlock(IReadOnlyList<DailyDigestEmail> emails)
  {
    return string.Join(
      '\n',
      emails.Select(email => $"{email.CandidateId}. [{email.DateUtc:yyyy-MM-dd HH:mm}Z] From: {email.From} | Subject: {email.Subject}"));
  }

  private sealed record DailyDigestEmail(
    int CandidateId,
    string Subject,
    string From,
    DateTime DateUtc,
    string DigestInput,
    string NormalizationInput);
}
