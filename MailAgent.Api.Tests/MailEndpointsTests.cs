using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Digest.Models;
using MailAgent.Api.Endpoints;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Digest;
using MailAgent.Application.Import;
using MailAgent.Application.Llm;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace MailAgent.Api.Tests;

[TestFixture]
public class MailEndpointsTests
{
  private IMailClient _mailClient = null!;
  private IMailRepository _mailRepository = null!;
  private IDailyDigestRepository _dailyDigestRepository = null!;
  private ILlmClient _llmClient = null!;

  [SetUp]
  public void SetUp()
  {
    _mailClient = Substitute.For<IMailClient>();
    _mailRepository = Substitute.For<IMailRepository>();
    _dailyDigestRepository = Substitute.For<IDailyDigestRepository>();
    _llmClient = Substitute.For<ILlmClient>();
  }

  [Test]
  public async Task GetFolders_ReturnsFolderNamesFromMailClient()
  {
    // Given.
    var folders = new[] { "Releases", "Alerts" };
    _mailClient.GetInboxSubfolderNamesAsync(Arg.Any<CancellationToken>()).Returns(folders);

    using var client = await CreateClientAsync();

    // When.
    var response = await client.GetAsync("/folders");

    // Then.
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    Assert.That(payload["folders"]!.AsArray().Select(x => x!.GetValue<string>()), Is.EqualTo(folders));
  }

  [Test]
  public async Task GetMails_ReturnsLatestMessagesWithNormalizedMessageIds()
  {
    // Given.
    var date = DateTimeOffset.Parse("2026-03-16T10:00:00Z");
    _mailClient.GetLatestFromFolderAsync("/", 5, Arg.Any<CancellationToken>())
      .Returns([
        new MailMessage("ext-1", "  message-id  ", "Subject", "from@example.com", date, null, "body"),
      ]);

    using var client = await CreateClientAsync();

    // When.
    var response = await client.GetAsync("/mails");

    // Then.
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    Assert.Multiple(() =>
    {
      Assert.That(payload["total"]!.GetValue<int>(), Is.EqualTo(1));
      Assert.That(payload["latest"]![0]!["messageId"]!.GetValue<string>(), Is.EqualTo("message-id"));
      Assert.That(payload["latest"]![0]!["subject"]!.GetValue<string>(), Is.EqualTo("Subject"));
      Assert.That(payload["latest"]![0]!["from"]!.GetValue<string>(), Is.EqualTo("from@example.com"));
      Assert.That(payload["latest"]![0]!["date"]!.GetValue<string>(), Is.EqualTo(date.ToString("u")));
    });
  }

  [Test]
  public async Task ImportMails_ReturnsImportResultFromService()
  {
    // Given.
    _mailClient.GetLatestFromFolderAsync("Releases", 5, Arg.Any<CancellationToken>())
      .Returns([
        new MailMessage("ext-1", " message-id ", "Release", "from@example.com", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "<p>body</p>", null),
      ]);

    using var client = await CreateClientAsync();

    // When.
    var response = await client.PostAsJsonAsync("/mails/import", new { folder = "Releases", takeCount = 5 });

    // Then.
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    await _mailRepository.Received(1).SaveNewAsync(Arg.Is<IReadOnlyCollection<StoredMail>>(mails => mails.Count == 1), Arg.Any<CancellationToken>());

    var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    Assert.Multiple(() =>
    {
      Assert.That(payload["total"]!.GetValue<int>(), Is.EqualTo(1));
      Assert.That(payload["latest"]![0]!["messageId"]!.GetValue<string>(), Is.EqualTo("message-id"));
      Assert.That(payload["latest"]![0]!["body"]!.GetValue<string>(), Is.EqualTo("body"));
    });
  }

  [Test]
  public async Task GetDigest_ReturnsDigestResultFromService()
  {
    // Given.
    _mailRepository.GetByPeriodFromFolder("Releases", TimeSpan.FromHours(1), Arg.Any<CancellationToken>())
      .Returns([
        new StoredMail(0, "Releases", "message-id", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "from@example.com", "Service release", "raw", "release body", "2026-03-16 10:00:00Z"),
      ]);

    _llmClient.Generate(Arg.Any<LlmGenerateRequest>(), Arg.Any<CancellationToken>())
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse(" digest text "));

    using var client = await CreateClientAsync();

    // When.
    var response = await client.GetAsync("/digest?folder=Releases&period=01:00:00");

    // Then.
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    Assert.Multiple(() =>
    {
      Assert.That(payload["totalFetched"]!.GetValue<int>(), Is.EqualTo(1));
      Assert.That(payload["selected"]!.GetValue<int>(), Is.EqualTo(1));
      Assert.That(payload["digest"]!.GetValue<string>(), Is.EqualTo("digest text"));
    });
  }

  [Test]
  public async Task GetDailyDigests_ReturnsLatestPersistedDigests()
  {
    // Given.
    _dailyDigestRepository
      .GetLatest("Releases", 10, Arg.Any<CancellationToken>())
      .Returns([
        new DailyDigest(
          1,
          "Releases",
          new DateOnly(2026, 4, 10),
          20,
          3,
          "# Release Digest",
          DateTimeOffset.Parse("2026-04-11T08:00:00Z"))
      ]);

    using var client = await CreateClientAsync();

    // When.
    var response = await client.GetAsync("/daily-digests?folder=Releases&take=10");

    // Then.
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    Assert.Multiple(() =>
    {
      Assert.That(payload["items"]!.AsArray().Count, Is.EqualTo(1));
      Assert.That(payload["items"]![0]!["folder"]!.GetValue<string>(), Is.EqualTo("Releases"));
      Assert.That(payload["items"]![0]!["digestDate"]!.GetValue<string>(), Is.EqualTo("2026-04-10"));
    });
  }

  [Test]
  public async Task GetDailyDigestByDate_ReturnsPersistedDigestDocument()
  {
    // Given.
    _dailyDigestRepository
      .GetByDate("Releases", new DateOnly(2026, 4, 10), Arg.Any<CancellationToken>())
      .Returns(new DailyDigest(
        1,
        "Releases",
        new DateOnly(2026, 4, 10),
        20,
        3,
        "# Release Digest",
        DateTimeOffset.Parse("2026-04-11T08:00:00Z")));

    using var client = await CreateClientAsync();

    // When.
    var response = await client.GetAsync("/daily-digests/2026-04-10?folder=Releases");

    // Then.
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    Assert.Multiple(() =>
    {
      Assert.That(payload["folder"]!.GetValue<string>(), Is.EqualTo("Releases"));
      Assert.That(payload["digestMarkdown"]!.GetValue<string>(), Is.EqualTo("# Release Digest"));
      Assert.That(payload["selected"]!.GetValue<int>(), Is.EqualTo(3));
    });
  }

  private async Task<HttpClient> CreateClientAsync()
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();

    builder.Services.AddSingleton(_mailClient);
    builder.Services.AddSingleton(_mailRepository);
    builder.Services.AddSingleton(_dailyDigestRepository);
    builder.Services.AddSingleton(_llmClient);
    builder.Services.AddSingleton(new LlmSettings
    {
      Provider = LlmProvider.Ollama,
      BaseUrl = "http://localhost:11434/",
      Timeout = TimeSpan.FromMinutes(5),
      FastModel = "llama3.2:3b",
      MainModel = "qwen2.5:7b-instruct",
    });
    builder.Services.AddSingleton<EmailBodyConverter>();
    builder.Services.AddScoped<MailImportService>();
    builder.Services.AddScoped<ReleaseDigestService>();

    var app = builder.Build();
    app.MapMailEndpoints();
    await app.StartAsync();

    return app.GetTestClient();
  }
}
