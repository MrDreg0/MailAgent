using AutoFixture;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Digest;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MailAgent.Application.Tests;

[TestFixture]
public class DailyDigestServiceTests
{
  private Fixture _fixture = null!;
  private IMailRepository _mailRepository = null!;
  private ILlmClient _llmClient = null!;
  private DailyDigestService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _mailRepository = Substitute.For<IMailRepository>();
    _llmClient = Substitute.For<ILlmClient>();
    _sut = new DailyDigestService(_mailRepository, _llmClient, CreateLlmSettings(), NullLogger<DailyDigestService>.Instance);
  }

  [Test]
  public async Task BuildForDate_UsesUtcRangeForConfiguredBusinessDay()
  {
    // Given.
    var digestDate = new DateOnly(2026, 4, 10);
    const string folderName = "Releases";
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    var storedMails = new[]
    {
      CreateStoredMail(subject: "General update", markdownBody: "noise"),
      CreateStoredMail(subject: "Product release", markdownBody: new string('a', 1605))
    };

    _mailRepository
      .GetByUtcRangeFromFolder(
        folderName,
        DateTimeOffset.Parse("2026-04-10T00:00:00Z"),
        DateTimeOffset.Parse("2026-04-11T00:00:00Z"),
        cancellationToken)
      .Returns(storedMails);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new LlmGenerateResponse("2"),
        new LlmGenerateResponse("  # Release Digest for 2026-04-10\n\n## Highlights\n- Important release  "));

    // When.
    var result = await _sut.BuildForDate(folderName, digestDate, cancellationToken);

    // Then.
    await _mailRepository.Received(1).GetByUtcRangeFromFolder(
      folderName,
      DateTimeOffset.Parse("2026-04-10T00:00:00Z"),
      DateTimeOffset.Parse("2026-04-11T00:00:00Z"),
      cancellationToken);

    Assert.Multiple(() =>
    {
      Assert.That(result.Folder, Is.EqualTo(folderName));
      Assert.That(result.DigestDate, Is.EqualTo(digestDate));
      Assert.That(result.TotalFetched, Is.EqualTo(2));
      Assert.That(result.Selected, Is.EqualTo(1));
      Assert.That(result.DigestMarkdown, Does.StartWith("# Release Digest for 2026-04-10"));
      Assert.That(requests, Has.Count.EqualTo(2));
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Product release"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Subject: General update"));
      Assert.That(requests[1].Prompt, Does.Contain($"Body preview: {new string('a', 1500)}"));
      Assert.That(requests[1].Prompt, Does.Not.Contain(new string('a', 1501)));
    });
  }

  [Test]
  public async Task BuildForDate_ReturnsStaticEmptyDigest_WhenNoReleaseEmailsWereSelected()
  {
    // Given.
    var digestDate = new DateOnly(2026, 4, 10);

    _mailRepository
      .GetByUtcRangeFromFolder(
        "Releases",
        Arg.Any<DateTimeOffset>(),
        Arg.Any<DateTimeOffset>(),
        Arg.Any<CancellationToken>())
      .Returns([
        CreateStoredMail(subject: "General update", markdownBody: "body")
      ]);

    _llmClient
      .Generate(Arg.Any<LlmGenerateRequest>(), Arg.Any<CancellationToken>())
      .Returns(new LlmGenerateResponse(string.Empty));

    // When.
    var result = await _sut.BuildForDate("Releases", digestDate);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.TotalFetched, Is.EqualTo(1));
      Assert.That(result.Selected, Is.EqualTo(0));
      Assert.That(result.DigestMarkdown, Does.Contain("No release mails were selected for this day."));
    });

    await _llmClient.Received(1).Generate(Arg.Any<LlmGenerateRequest>(), Arg.Any<CancellationToken>());
  }

  private StoredMail CreateStoredMail(
    string subject,
    string markdownBody)
  {
    return _fixture.Build<StoredMail>()
      .With(x => x.Id, _fixture.Create<int>())
      .With(x => x.Folder, "Releases")
      .With(x => x.MessageId, _fixture.Create<string>())
      .With(x => x.RawBody, _fixture.Create<string>())
      .With(x => x.Subject, subject)
      .With(x => x.MarkdownBody, markdownBody)
      .Create();
  }

  private static LlmSettings CreateLlmSettings()
  {
    return new LlmSettings
    {
      Provider = LlmProvider.Ollama,
      BaseUrl = "http://localhost:11434/",
      Timeout = TimeSpan.FromMinutes(5),
      FastModel = "llama3.2:3b",
      MainModel = "qwen2.5:7b-instruct",
    };
  }
}
