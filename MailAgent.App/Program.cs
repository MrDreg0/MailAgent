using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailAgent.Database.PostgreSql;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MessageSummaryItems = MailKit.MessageSummaryItems;

var webApplicationBuilder = WebApplication.CreateBuilder(args);

var connectionString = webApplicationBuilder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Database connection string is missing");
webApplicationBuilder.Services.AddPostgreSqlDataContext(connectionString);

webApplicationBuilder.Services.AddHttpClient("ollama", client =>
{
  client.BaseAddress = new Uri("http://localhost:11434/");
  client.Timeout = TimeSpan.FromMinutes(5);
});

var webApplication = webApplicationBuilder.Build();

var mailHost = webApplication.Configuration["Host"];
var mailPort = webApplication.Configuration["Port"] ?? throw new InvalidOperationException("Mail port configuration is missing");
var username = webApplication.Configuration["Username"];
var password = webApplication.Configuration["Password"];

webApplication.MapGet("/folders", async () =>
{
  using var imapClient = new ImapClient();

  await imapClient.ConnectAsync(mailHost, int.Parse(mailPort), SecureSocketOptions.SslOnConnect);
  await imapClient.AuthenticateAsync(username, password);
  
  var inboxFolder = imapClient.Inbox;

  var folders = await inboxFolder.GetSubfoldersAsync();
  
  var folderNames = folders.Select(f => f.Name);
  
  await imapClient.DisconnectAsync(true);
  
  return Results.Ok(new
  {
    Folders = folderNames,
  });
});

webApplication.MapGet("/test-mail", async () =>
{
  using var imapClient = new ImapClient();

  await imapClient.ConnectAsync(mailHost, int.Parse(mailPort), SecureSocketOptions.SslOnConnect);
  await imapClient.AuthenticateAsync(username, password);

  var inboxFolder = imapClient.Inbox;
  await inboxFolder.OpenAsync(MailKit.FolderAccess.ReadOnly);

  const int takeCount = 5;

  var summaries = await inboxFolder.FetchAsync(
    inboxFolder.Count - takeCount, 
    -1,
    new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope)
  );
  
  var messageSummaries = new List<object>(capacity: takeCount);

  foreach (var summary in summaries.TakeLast(takeCount))
  {
    var message = await inboxFolder.GetMessageAsync(summary.UniqueId);

    messageSummaries.Add(new
    {
      summary.UniqueId,
      message.MessageId,
      message.Subject,
      From = message.From.ToString(),
      Date = message.Date.ToString("u")
    });
  }

  await imapClient.DisconnectAsync(true);

  return Results.Ok(new
  {
    Total = messageSummaries.Count,
    Latest = messageSummaries,
  });
});

webApplication.MapGet("/digest", async (IHttpClientFactory httpClientFactory) =>
{
  const int takeCount = 10;
  var emails = new List<EmailDto>(capacity: takeCount);

  using (var imapClient = new ImapClient())
  {
    await imapClient.ConnectAsync(mailHost, int.Parse(mailPort), SecureSocketOptions.SslOnConnect);
    await imapClient.AuthenticateAsync(username, password);

    var inboxFolder = imapClient.Inbox;
    await inboxFolder.OpenAsync(MailKit.FolderAccess.ReadOnly);

    var totalMessageCount = inboxFolder.Count;
    var actualTakeCount = Math.Min(takeCount, totalMessageCount);

    for (var indexFromEnd = 0; indexFromEnd < actualTakeCount; indexFromEnd++)
    {
      var messageIndex = totalMessageCount - 1 - indexFromEnd;
      var message = await inboxFolder.GetMessageAsync(messageIndex);

      var bodyText = message.TextBody;
      if (string.IsNullOrWhiteSpace(bodyText) && !string.IsNullOrWhiteSpace(message.HtmlBody))
      {
        // супер-простой strip html для прототипа
        bodyText = StripHtml(message.HtmlBody);
      }

      // ограничим размер тела, чтобы не кормить LLM лишним
      bodyText = Truncate(bodyText ?? string.Empty, 1500);

      emails.Add(new EmailDto(
        indexFromEnd + 1,
        message.Subject ?? string.Empty,
        message.From.ToString(),
        message.Date.UtcDateTime,
        bodyText
      ));
    }

    await imapClient.DisconnectAsync(true);
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

static string Truncate(string value, int maxLength) 
  => value.Length <= maxLength ? value : value[..maxLength];

static string StripHtml(string html)
{
  // сверх-простой прототип: убираем теги
  var charArray = new char[html.Length];
  var charArrayIndex = 0;
  var inside = false;

  foreach (var ch in html)
  {
    switch (ch)
    {
      case '<':
        inside = true;
        continue;
      case '>':
        inside = false;
        continue;
    }

    if (!inside)
    {
      charArray[charArrayIndex] = ch;
      charArrayIndex++;
    }
  }

  return new string(charArray, 0, charArrayIndex);
}

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
