using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MailAgent.Api.Endpoints;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Contracts.Ollama;
using MailAgent.Application.Digest;
using MailAgent.Application.Import;
using MailAgent.Application.Ollama;
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
  private IOllamaClient _ollamaClient = null!;

  [SetUp]
  public void SetUp()
  {
    _mailClient = Substitute.For<IMailClient>();
    _mailRepository = Substitute.For<IMailRepository>();
    _ollamaClient = Substitute.For<IOllamaClient>();
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

    _ollamaClient.Generate(Arg.Any<OllamaGenerateRequest>(), Arg.Any<CancellationToken>())
      .Returns(
        new OllamaGenerateResponse("1"),
        new OllamaGenerateResponse(" digest text "));

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

  private async Task<HttpClient> CreateClientAsync()
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();

    builder.Services.AddSingleton(_mailClient);
    builder.Services.AddSingleton(_mailRepository);
    builder.Services.AddSingleton(_ollamaClient);
    builder.Services.AddSingleton<EmailBodyConverter>();
    builder.Services.AddScoped<MailImportService>();
    builder.Services.AddScoped<ReleaseDigestService>();

    var app = builder.Build();
    app.MapMailEndpoints();
    await app.StartAsync();

    return app.GetTestClient();
  }
}
