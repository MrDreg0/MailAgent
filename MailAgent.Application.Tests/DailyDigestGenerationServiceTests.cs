using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Digest.Models;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Digest;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MailAgent.Application.Tests;

[TestFixture]
public class DailyDigestGenerationServiceTests
{
  private IDailyDigestRepository _dailyDigestRepository = null!;
  private IMailRepository _mailRepository = null!;
  private ILlmClient _llmClient = null!;
  private DailyDigestService _dailyDigestService = null!;
  private DailyDigestGenerationService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _dailyDigestRepository = Substitute.For<IDailyDigestRepository>();
    _mailRepository = Substitute.For<IMailRepository>();
    _llmClient = Substitute.For<ILlmClient>();
    _dailyDigestService = new DailyDigestService(
      _mailRepository,
      _llmClient,
      new LlmSettings
      {
        Provider = LlmProvider.Ollama,
        BaseUrl = "http://localhost:11434/",
        Timeout = TimeSpan.FromMinutes(5),
        FastModel = "llama3.2:3b",
        MainModel = "qwen3.5-9b"
      },
      Substitute.For<ILogger<DailyDigestService>>());
    _sut = new DailyDigestGenerationService(_dailyDigestRepository, _dailyDigestService, Substitute.For<ILogger<DailyDigestGenerationService>>());
  }

  [Test]
  public async Task Regenerate_BuildsAndStoresDigest_WithoutCheckingExistingDocument()
  {
    // Given.
    var digestDate = new DateOnly(2026, 4, 10);
    _mailRepository
      .GetByUtcRangeFromFolder(
        "Releases",
        DateTimeOffset.Parse("2026-04-10T00:00:00Z"),
        DateTimeOffset.Parse("2026-04-11T00:00:00Z"),
        Arg.Any<CancellationToken>())
      .Returns([
        new StoredMail(
          Id: 1,
          Folder: "Releases",
          MessageId: "message-id",
          DateUtc: DateTimeOffset.Parse("2026-04-10T10:00:00Z"),
          From: "from@example.com",
          Subject: "Service release",
          RawBody: "raw",
          MarkdownBody: "release body",
          InsertedAt: "2026-04-10 10:00:00Z")
      ]);

    _llmClient
      .Generate(Arg.Any<LlmGenerateRequest>(), Arg.Any<CancellationToken>())
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse("# Regenerated digest"));

    // When.
    var result = await _sut.Regenerate("Releases", digestDate);

    // Then.
    await _dailyDigestRepository.DidNotReceive().GetByDate("Releases", digestDate, Arg.Any<CancellationToken>());
    await _dailyDigestRepository.Received(1).Save(
      Arg.Is<DailyDigest>(digest =>
        digest.Folder == "Releases" &&
        digest.DigestDate == digestDate &&
        digest.TotalFetched == 1 &&
        digest.Selected == 1 &&
        digest.DigestMarkdown == "# Regenerated digest"),
      Arg.Any<CancellationToken>());

    Assert.Multiple(() =>
    {
      Assert.That(result.Folder, Is.EqualTo("Releases"));
      Assert.That(result.DigestDate, Is.EqualTo(digestDate));
      Assert.That(result.DigestMarkdown, Is.EqualTo("# Regenerated digest"));
    });
  }
}
