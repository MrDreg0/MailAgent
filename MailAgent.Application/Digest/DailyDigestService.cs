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

  private static string BuildDigestPrompt(DateOnly digestDate, IReadOnlyList<DailyDigestEmail> selected)
  {
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine($"Ты готовишь короткий утренний markdown-дайджест релизов за {digestDate:yyyy-MM-dd}.");
    stringBuilder.AppendLine("Цель: помочь быстро понять самые важные изменения за день без чтения всех писем.");
    stringBuilder.AppendLine("Верни только markdown, без пояснений вне структуры.");
    stringBuilder.AppendLine("Структура ответа строго такая:");
    stringBuilder.AppendLine($"# Release Digest for {digestDate:yyyy-MM-dd}");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("## Highlights");
    stringBuilder.AppendLine("- максимум 3 коротких пункта с самым важным за день.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("## Releases");
    stringBuilder.AppendLine("### Product or Service - Version");
    stringBuilder.AppendLine("- 1-2 коротких полезных пункта по сути изменений.");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Правила:");
    stringBuilder.AppendLine("- Не выдумывай версии, детали или причины важности, которых нет в письмах.");
    stringBuilder.AppendLine("- Не добавляй source, from, date, ссылки, release notes, docker-образы, пути к файлам, порталы, номера задач, work item id и другой служебный шум.");
    stringBuilder.AppendLine("- Не используй эмодзи, маркетинговый стиль и слова вроде 'срочно', 'критично' или 'важно', если это не следует напрямую из письма.");
    stringBuilder.AppendLine("- Не повторяй один и тот же факт дословно в Highlights и Releases.");
    stringBuilder.AppendLine("- Если в письме только факт выхода версии без деталей, так и напиши кратко: версия вышла, подробности в письме не раскрыты.");
    stringBuilder.AppendLine("- Объединяй связанные письма в один блок, если это один продукт или одна версия, например продукт и его installer.");
    stringBuilder.AppendLine("- Если за день есть много однотипных сервисных обновлений с одинаковым security fix, сгруппируй их в один общий блок вместо длинного списка почти одинаковых секций.");
    stringBuilder.AppendLine("- Для каждого блока сначала пытайся выделить пользовательский эффект, исправление или суть изменения. Артефакты поставки и инфраструктурные детали упоминай только если это единственная полезная информация в письме.");
    stringBuilder.AppendLine("- Не делай отдельный highlight только про наличие ссылки, release notes, веб-клиента или артефактов поставки.");
    stringBuilder.AppendLine("- Если есть основная версия продукта и отдельное письмо про installer, опиши это как один релиз и коротко упомяни, что вместе с ним обновлен installer.");
    stringBuilder.AppendLine("- Не пиши фразы вроде 'веб-клиент доступен по ссылке', 'подробности в release notes', 'доступны пакеты/образы/утилиты' и другие формулировки про доставку артефактов, если это не единственная содержательная информация письма.");
    stringBuilder.AppendLine("- Если письмо почти целиком про ссылку, release notes, портал релизов, установочные пакеты или Docker-образы, не выноси это в digest. Вместо этого кратко зафиксируй сам факт выхода версии, если он действительно был.");
    stringBuilder.AppendLine("- Если письмо сообщает о новой версии продукта, а остальной текст сводится к ссылке, веб-клиенту или способу доступа, оставь только факт обновления версии без упоминания ссылки, веб-клиента и доступности.");
    stringBuilder.AppendLine("- Для Highlights выбирай только то, что отвечает на вопрос 'что реально изменилось за день?'. Не добавляй туда доступность артефактов, installer, ссылки или web client availability.");
    stringBuilder.AppendLine("- В разделе Releases должно быть не больше 5 секций. Оставляй только самые полезные для чтения утром.");
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
    stringBuilder.AppendLine("Убери дубли, сократи шум и оставь только то, что полезно прочитать утром.");
    stringBuilder.AppendLine("Сохрани строгую структуру:");
    stringBuilder.AppendLine($"# Release Digest for {digestDate:yyyy-MM-dd}");
    stringBuilder.AppendLine("## Highlights");
    stringBuilder.AppendLine("## Releases");
    stringBuilder.AppendLine("### Product or Service - Version");
    stringBuilder.AppendLine("- ...");
    stringBuilder.AppendLine();
    stringBuilder.AppendLine("Правила:");
    stringBuilder.AppendLine("- Максимум 3 highlights.");
    stringBuilder.AppendLine("- Максимум 5 release sections.");
    stringBuilder.AppendLine("- Объединяй related entries про один продукт, одну версию или одну волну однотипных security updates.");
    stringBuilder.AppendLine("- Если несколько сервисов обновлены одинаковым security fix в один день, сведи их в один общий блок вроде platform services / security updates.");
    stringBuilder.AppendLine("- Не добавляй source, from, date, ссылки, release notes, docker-образы, пути к файлам, номера задач, эмодзи и служебный шум.");
    stringBuilder.AppendLine("- Не повторяй одинаковые факты в нескольких секциях.");
    stringBuilder.AppendLine("- Если подробностей мало, пиши кратко и честно.");
    stringBuilder.AppendLine("- Предпочитай смысл изменения, а не артефакты поставки. Не делай highlight'ы про ссылки, release notes и docker-образы.");
    stringBuilder.AppendLine("- Если есть версия продукта и отдельно installer той же версии, оставь один объединенный блок.");
    stringBuilder.AppendLine("- Удаляй фразы про 'веб-клиент доступен по ссылке', 'подробности в release notes', 'доступны пакеты/образы/утилиты', если они не описывают суть изменения.");
    stringBuilder.AppendLine("- Если в итоговом тексте осталась только информация про ссылки, портал релизов, release notes или артефакты поставки, сократи ее до простого факта выхода версии либо выброси как шум.");
    stringBuilder.AppendLine("- Если блок про продукт по сути говорит только 'вышла новая версия, доступен веб-клиент/ссылка', перепиши его в краткий факт обновления версии без упоминания ссылки, веб-клиента и доступности.");
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
