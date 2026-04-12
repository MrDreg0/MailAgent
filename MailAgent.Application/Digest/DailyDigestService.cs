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
  DailyDigestSettings dailyDigestSettings,
  ILogger<DailyDigestService> logger)
{
  private const int ClassifierBatchSize = 50;
  private const int DigestBatchSize = 5;
  private const int BodyPreviewMaxLength = 1500;
  private const string VersionChangesHeading = "# Изменения версии";
  private static readonly HashSet<string> PreferredDigestSectionHeadings =
  [
    "## Общие",
    "## Общее",
    "## Новые возможности",
    "## Исправленные замечания",
    "## Безопасность",
    "## БД",
    "## Компонента"
  ];
  private static readonly HashSet<string> IgnoredDigestSectionHeadings =
  [
    "## Dockerfile",
  ];

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
      let bodyPreview = BuildBodyPreview(message.MarkdownBody)
      select new DailyDigestEmail(
        CandidateId: candidateId++,
        Subject: message.Subject,
        From: message.From,
        DateUtc: message.DateUtc.UtcDateTime,
        BodyPreview: bodyPreview));

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

  private static string BuildBodyPreview(string markdownBody)
  {
    if (string.IsNullOrWhiteSpace(markdownBody))
    {
      return string.Empty;
    }

    var normalizedMarkdown = markdownBody.Replace("\r\n", "\n").Trim();
    var prioritizedPreview = ExtractRelevantDigestSections(normalizedMarkdown);

    return Truncate(
      string.IsNullOrWhiteSpace(prioritizedPreview)
        ? normalizedMarkdown
        : prioritizedPreview,
      BodyPreviewMaxLength).Trim();
  }

  private static string ExtractRelevantDigestSections(string markdownBody)
  {
    var versionChangesSection = TryExtractTopLevelSection(markdownBody, VersionChangesHeading);

    if (!string.IsNullOrWhiteSpace(versionChangesSection))
    {
      var preferredVersionBlocks = ExtractSecondLevelBlocks(versionChangesSection, includeFallbackBlocks: false);
      if (!string.IsNullOrWhiteSpace(preferredVersionBlocks))
      {
        return $"{VersionChangesHeading}\n\n{preferredVersionBlocks}".Trim();
      }

      var fallbackVersionBlocks = ExtractSecondLevelBlocks(versionChangesSection, includeFallbackBlocks: true);
      if (!string.IsNullOrWhiteSpace(fallbackVersionBlocks))
      {
        return $"{VersionChangesHeading}\n\n{fallbackVersionBlocks}".Trim();
      }

      return versionChangesSection;
    }

    var preferredBlocks = ExtractSecondLevelBlocks(markdownBody, includeFallbackBlocks: false);
    if (!string.IsNullOrWhiteSpace(preferredBlocks))
    {
      return preferredBlocks;
    }

    return ExtractSecondLevelBlocks(markdownBody, includeFallbackBlocks: true);
  }

  private static string? TryExtractTopLevelSection(string markdownBody, string heading)
  {
    var lines = markdownBody.Split('\n');
    var startIndex = FindLineIndex(lines, heading);
    if (startIndex < 0)
    {
      return null;
    }

    var endIndex = lines.Length;
    for (var index = startIndex + 1; index < lines.Length; index++)
    {
      if (lines[index].StartsWith("# ", StringComparison.Ordinal))
      {
        endIndex = index;
        break;
      }
    }

    return string.Join('\n', lines[startIndex..endIndex]).Trim();
  }

  private static string ExtractSecondLevelBlocks(string markdownBody, bool includeFallbackBlocks)
  {
    var lines = markdownBody.Split('\n');
    var blocks = new List<string>();

    for (var index = 0; index < lines.Length; index++)
    {
      if (!lines[index].StartsWith("## ", StringComparison.Ordinal))
      {
        continue;
      }

      var heading = lines[index].Trim();
      if (!ShouldIncludeDigestSection(heading, includeFallbackBlocks))
      {
        continue;
      }

      var endIndex = lines.Length;
      for (var nextIndex = index + 1; nextIndex < lines.Length; nextIndex++)
      {
        if (lines[nextIndex].StartsWith("# ", StringComparison.Ordinal)
          || lines[nextIndex].StartsWith("## ", StringComparison.Ordinal))
        {
          endIndex = nextIndex;
          break;
        }
      }

      blocks.Add(string.Join('\n', lines[index..endIndex]).Trim());
      index = endIndex - 1;
    }

    return string.Join("\n\n", blocks.Where(block => !string.IsNullOrWhiteSpace(block))).Trim();
  }

  private static bool ShouldIncludeDigestSection(string heading, bool includeFallbackBlocks)
  {
    if (PreferredDigestSectionHeadings.Contains(heading))
    {
      return true;
    }

    return includeFallbackBlocks
      && !IgnoredDigestSectionHeadings.Contains(heading);
  }

  private static int FindLineIndex(string[] lines, string targetLine)
  {
    for (var index = 0; index < lines.Length; index++)
    {
      if (string.Equals(lines[index].Trim(), targetLine, StringComparison.Ordinal))
      {
        return index;
      }
    }

    return -1;
  }

  private string BuildEmptyDigestPrompt(DateOnly digestDate)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"Create a short markdown daily release digest for {digestDate:yyyy-MM-dd} when no release emails were selected.");
    stringBuilder.AppendLine($"Write the final digest in {dailyDigestSettings.OutputLanguage}.");
    stringBuilder.AppendLine($"All headings and bullet points must be in {dailyDigestSettings.OutputLanguage}. Do not mix languages in the final digest.");
    stringBuilder.AppendLine("Return markdown only.");
    stringBuilder.AppendLine("Use this exact logical structure with localized headings:");
    stringBuilder.AppendLine($"- digest title for {digestDate:yyyy-MM-dd}");
    stringBuilder.AppendLine("- highlights section with one short bullet saying no release emails were selected for this day");
    stringBuilder.AppendLine("- releases section with one short bullet saying there are no release entries for this day");

    return stringBuilder.ToString();
  }

  private string BuildDigestPrompt(DateOnly digestDate, IReadOnlyList<DailyDigestEmail> selected)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"You are preparing a short morning markdown release digest for {digestDate:yyyy-MM-dd}.");
    stringBuilder.AppendLine("Goal: help the reader quickly understand the most important changes of the day without reading every email.");
    stringBuilder.AppendLine($"Write the final digest in {dailyDigestSettings.OutputLanguage}.");
    stringBuilder.AppendLine($"All headings and bullet points must be in {dailyDigestSettings.OutputLanguage}. Do not mix languages in the final digest.");
    stringBuilder.AppendLine("Return markdown only, with no explanations outside the document.");
    stringBuilder.AppendLine("The markdown structure must be:");
    stringBuilder.AppendLine($"# <localized digest title for {digestDate:yyyy-MM-dd}>");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("## <localized highlights heading>");
    stringBuilder.AppendLine("- maximum 3 short bullets with the most important changes of the day.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("## <localized releases heading>");
    stringBuilder.AppendLine("### Product or Service - Version");
    stringBuilder.AppendLine("- 1-2 short useful bullets about the actual substance of the change.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Rules:");
    stringBuilder.AppendLine("- Do not invent versions, details, or reasons for importance that are not present in the emails.");
    stringBuilder.AppendLine("- Do not add source, from, date, links, release notes, docker images, file paths, portals, task numbers, work item ids, or other delivery noise.");
    stringBuilder.AppendLine("- Do not use emoji, marketing language, or words like 'urgent', 'critical', or 'important' unless the email explicitly supports that wording.");
    stringBuilder.AppendLine("- Do not repeat the same fact verbatim in both Highlights and Releases.");
    stringBuilder.AppendLine("- If an email only announces that a version was released but does not describe the changes, say that briefly and honestly.");
    stringBuilder.AppendLine("- Merge related emails into one block when they describe the same product or the same version, for example a product release and its installer.");
    stringBuilder.AppendLine("- If the day contains many similar service updates with the same security fix, group them into one shared block instead of a long list of nearly identical sections.");
    stringBuilder.AppendLine("- For each release block, first prefer the user-visible effect, the fix, or the substance of the change. Mention delivery artifacts and infrastructure details only if they are the only useful information in the email.");
    stringBuilder.AppendLine("- Do not create a separate highlight just because a link, release notes, a web client, or delivery artifacts are available.");
    stringBuilder.AppendLine("- If there is a main product version and a separate installer email for the same version, describe them as one release and mention the installer briefly.");
    stringBuilder.AppendLine("- Do not write phrases like 'the web client is available via the link', 'details are available in release notes', or 'packages/images/utilities are available' unless that is the only substantive information in the email.");
    stringBuilder.AppendLine("- If an email is mostly about links, release notes, a release portal, installation packages, or docker images, do not surface that as digest content. Instead, briefly record the version release itself if that is the real event.");
    stringBuilder.AppendLine("- If an email announces a new product version and the rest of the text is just about a link, a web client, or access instructions, keep only the fact of the version update and omit the link, web client, and availability wording.");
    stringBuilder.AppendLine("- For Highlights, only choose items that answer the question 'what actually changed today?'. Do not include artifact availability, installer availability, links, or web client availability.");
    stringBuilder.AppendLine("- The Releases section must contain no more than 5 sections. Keep only the items that are most useful for a quick morning read.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Emails:");

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

  private string BuildDigestMergePrompt(DateOnly digestDate, IReadOnlyList<string> partialDigests)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"Merge partial markdown release digests for {digestDate:yyyy-MM-dd} into one final markdown document.");
    stringBuilder.AppendLine("Remove duplicates, cut noise, and keep only what is useful for a quick morning read.");
    stringBuilder.AppendLine($"Write the final digest in {dailyDigestSettings.OutputLanguage}.");
    stringBuilder.AppendLine($"All headings and bullet points must be in {dailyDigestSettings.OutputLanguage}. Do not mix languages in the final digest.");
    stringBuilder.AppendLine("Keep this structure:");
    stringBuilder.AppendLine($"# <localized digest title for {digestDate:yyyy-MM-dd}>");
    stringBuilder.AppendLine("## <localized highlights heading>");
    stringBuilder.AppendLine("## <localized releases heading>");
    stringBuilder.AppendLine("### Product or Service - Version");
    stringBuilder.AppendLine("- ...");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Rules:");
    stringBuilder.AppendLine("- Maximum 3 highlights.");
    stringBuilder.AppendLine("- Maximum 5 release sections.");
    stringBuilder.AppendLine("- Merge related entries about the same product, the same version, or one wave of similar security updates.");
    stringBuilder.AppendLine("- If multiple services were updated with the same security fix on the same day, collapse them into one shared block such as platform services / security updates.");
    stringBuilder.AppendLine("- Do not add source, from, date, links, release notes, docker images, file paths, task numbers, emoji, or delivery noise.");
    stringBuilder.AppendLine("- Do not repeat the same facts across multiple sections.");
    stringBuilder.AppendLine("- If details are sparse, be short and honest.");
    stringBuilder.AppendLine("- Prefer the substance of the change, not delivery artifacts. Do not create highlights about links, release notes, or docker images.");
    stringBuilder.AppendLine("- If there is a product version and a separate installer entry for the same version, keep one combined block.");
    stringBuilder.AppendLine("- Remove phrases like 'the web client is available via the link', 'details are available in release notes', or 'packages/images/utilities are available' when they do not describe the actual change.");
    stringBuilder.AppendLine("- If a block only conveys links, release portal references, release notes, or delivery artifacts, reduce it to the plain fact of the version release or drop it as noise.");
    stringBuilder.AppendLine("- If a product block basically says only 'a new version was released and a web client/link is available', rewrite it as a short fact about the version update and omit the link, web client, and availability wording.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Partial digests:");

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
    stringBuilder.AppendLine("You are an inbox filter. Select the emails that are about product releases, version announcements, or notable service updates.");
    stringBuilder.AppendLine("Return ONLY a comma-separated list of numeric Id values, with no extra text. If there are no release emails, return an empty string.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Emails:");

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
