using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailAgent;
using MailAgent.Mail;
using MailAgent.Database;
using MailAgent.Database.PostgreSql;
using MailAgent.Initialization;
using Microsoft.EntityFrameworkCore;

var webApplicationBuilder = WebApplication.CreateBuilder(args);

var connectionString = webApplicationBuilder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Database connection string is missing");
webApplicationBuilder.Services.AddPostgreSqlDataContext(connectionString);
webApplicationBuilder.Services.AddSingleton<EmailBodyConverter>();
webApplicationBuilder.Services.AddMailClient(webApplicationBuilder.Configuration);

webApplicationBuilder.Services.AddHttpClient("ollama", client =>
{
  client.BaseAddress = new Uri("http://localhost:11434/");
  client.Timeout = TimeSpan.FromMinutes(5);
});

var webApplication = webApplicationBuilder.Build();
webApplication.MapGet("/folders", async (IMailClient mailClient) =>
{
  var folderNames = await mailClient.GetInboxSubfolderNamesAsync();

  return Results.Ok(new
  {
    Folders = folderNames,
  });
});

webApplication.MapGet("/test-mail", async (PostgreSqlDataContext db, EmailBodyConverter bodyConverter, IMailClient mailClient) =>
{
  const string folderName = "Releases";
  const int takeCount = 5;

  var fetchedMessages = await mailClient.GetLatestFromFolderAsync(folderName, takeCount);
  var messageSummaries = new List<object>(capacity: fetchedMessages.Count);
  var mailCandidates = new List<MailDto>(capacity: fetchedMessages.Count);
  var externalIdHashes = new HashSet<int>(capacity: fetchedMessages.Count);
  var nonEmptyMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  foreach (var message in fetchedMessages)
  {
    var normalizedMessageId = message.MessageId.Trim();
    if (!string.IsNullOrWhiteSpace(normalizedMessageId))
    {
      nonEmptyMessageIds.Add(normalizedMessageId);
    }

    var externalIdHash = ToStableInt(message.ExternalId);
    externalIdHashes.Add(externalIdHash);

    var rawBody = message.HtmlBody ?? message.TextBody ?? string.Empty;
    var markdownBody = bodyConverter.ConvertToMarkdown(message.HtmlBody, message.TextBody);

    messageSummaries.Add(new
    {
      message.ExternalId,
      MessageId = normalizedMessageId,
      message.Subject,
      Body = markdownBody,
      message.From,
      Date = message.DateUtc.ToString("u")
    });

    mailCandidates.Add(new MailDto(
      Id: 0,
      Folder: folderName,
      ImapUid: externalIdHash,
      MessageId: normalizedMessageId,
      DateUtc: message.DateUtc.ToUniversalTime(),
      From: message.From,
      Subject: message.Subject,
      RawBody: rawBody,
      MarkdownBody: markdownBody,
      InsertedAt: DateTimeOffset.UtcNow.ToString("u")
    ));
  }

  if (mailCandidates.Count > 0)
  {
    var existingMessageIds = nonEmptyMessageIds.Count == 0
      ? new List<string>()
      : await db.Mails
        .Where(m => m.Folder == folderName && nonEmptyMessageIds.Contains(m.MessageId))
        .Select(m => m.MessageId)
        .ToListAsync();

    var existingExternalHashes = await db.Mails
      .Where(m => m.Folder == folderName && externalIdHashes.Contains(m.ImapUid))
      .Select(m => m.ImapUid)
      .ToListAsync();

    var existingMessageIdSet = existingMessageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var existingExternalHashSet = existingExternalHashes.ToHashSet();

    var newMails = mailCandidates.Where(candidate =>
    {
      if (!string.IsNullOrWhiteSpace(candidate.MessageId))
      {
        return !existingMessageIdSet.Contains(candidate.MessageId);
      }

      return !existingExternalHashSet.Contains(candidate.ImapUid);
    }).ToList();

    if (newMails.Count > 0)
    {
      await db.Mails.AddRangeAsync(newMails);
      await db.SaveChangesAsync();
    }
  }

  return Results.Ok(new
  {
    Total = messageSummaries.Count,
    Latest = messageSummaries,
  });
});

webApplication.MapGet("/digest", async (IHttpClientFactory httpClientFactory, EmailBodyConverter bodyConverter, IMailClient mailClient) =>
{
  const int takeCount = 10;
  var fetchedMessages = await mailClient.GetLatestFromInboxAsync(takeCount);
  var emails = new List<EmailDto>(capacity: fetchedMessages.Count);

  var emailId = 1;
  foreach (var message in fetchedMessages)
  {
    var bodyText = bodyConverter.ConvertToMarkdown(message.HtmlBody, message.TextBody);

    // ограничим размер тела, чтобы не кормить LLM лишним
    bodyText = Truncate(bodyText, 1500);

    emails.Add(new EmailDto(
      emailId++,
      message.Subject,
      message.From,
      message.DateUtc.UtcDateTime,
      bodyText
    ));
  }

  // 1) классификация релизных писем (одним батчем)
  var classifierPrompt = BuildClassifierPrompt(emails);

  var classifierClient = httpClientFactory.CreateClient("ollama");
  var classifierResponseText = await OllamaGenerateAsync(
    classifierClient,
    model: "llama3.2:3b",
    prompt: classifierPrompt
  );

  var selected = ParseSelectedIdsOrFallback(classifierResponseText, emails);

  // 2) сводка по выбранным письмам
  var digestPrompt = BuildDigestPrompt(selected);

  var summarizerClient = httpClientFactory.CreateClient("ollama");
  var digestText = await OllamaGenerateAsync(
    summarizerClient,
    model: "qwen2.5:7b-instruct",
    prompt: digestPrompt
  );

  return Results.Ok(new
  {
    TotalFetched = emails.Count,
    Selected = selected.Count,
    Digest = digestText.Trim(),
  });
});

webApplication.Run();
return;

static List<EmailDto> ParseSelectedIdsOrFallback(string classifierResponseText, IReadOnlyList<EmailDto> emails)
{
  // ожидаем: "1, 3, 5"
  var ids = new HashSet<int>();

  foreach (var part in classifierResponseText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
  {
    if (int.TryParse(part, out var id))
    {
      ids.Add(id);
    }
  }

  // fallback: если модель вернула мусор — выберем по простому правилу по subject
  if (ids.Count == 0 && !string.IsNullOrWhiteSpace(classifierResponseText))
  {
    return emails
      .Where(e => e.Subject.Contains("Вышла версия", StringComparison.OrdinalIgnoreCase)
        || e.Subject.Contains("release", StringComparison.OrdinalIgnoreCase)
        || e.Subject.Contains("version", StringComparison.OrdinalIgnoreCase))
      .ToList();
  }

  return emails.Where(e => ids.Contains(e.Id)).ToList();
}

static int ToStableInt(string value)
{
  unchecked
  {
    var hash = 23;
    foreach (var ch in value)
    {
      hash = (hash * 31) + ch;
    }

    return hash == int.MinValue ? 0 : Math.Abs(hash);
  }
}

static string Truncate(string value, int maxLength) 
  => value.Length <= maxLength ? value : value[..maxLength];

static string BuildDigestPrompt(IReadOnlyList<EmailDto> selected)
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

static string BuildClassifierPrompt(IReadOnlyList<EmailDto> emails)
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

async Task<string> OllamaGenerateAsync(HttpClient httpClient, string model, string prompt)
{
  var request = new OllamaGenerateRequest(
    model,
    prompt,
    false,
    0);

  var json = JsonSerializer.Serialize(request);
  using var content = new StringContent(json, Encoding.UTF8, "application/json");

  using var response = await httpClient.PostAsync("api/generate", content);
  response.EnsureSuccessStatusCode();

  var responseJson = await response.Content.ReadAsStringAsync();
  var parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);

  return parsed?.Response ?? string.Empty;
}

file sealed record EmailDto(int Id, string Subject, string From, DateTime DateUtc, string BodyPreview);

file sealed record OllamaGenerateRequest(
  [property: JsonPropertyName("model")] string Model,
  [property: JsonPropertyName("prompt")] string Prompt,
  [property: JsonPropertyName("stream")] bool Stream,
  [property: JsonPropertyName("keep_alive")] int KeepAlive);

file sealed record OllamaGenerateResponse(
  [property: JsonPropertyName("response")] string Response);
