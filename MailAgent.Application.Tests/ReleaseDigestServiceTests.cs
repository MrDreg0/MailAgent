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
public class ReleaseDigestServiceTests
{
  private Fixture _fixture = null!;
  private IMailRepository _mailRepository = null!;
  private ILlmClient _llmClient = null!;
  private ReleaseDigestService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _mailRepository = Substitute.For<IMailRepository>();
    _llmClient = Substitute.For<ILlmClient>();
    _sut = new ReleaseDigestService(_mailRepository, _llmClient, CreateLlmSettings(), NullLogger<ReleaseDigestService>.Instance);
  }

  [Test]
  public async Task BuildInboxDigestAsync_UsesClassifierIdsAndBuildsDigestFromSelectedEmails()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var period = _fixture.Create<TimeSpan>();
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    var selectedBody = new string('a', 1605);
    var storedMails = new[]
    {
      CreateStoredMail(subject: "Weekly news", markdownBody: "noise"),
      CreateStoredMail(subject: "Product release", markdownBody: selectedBody)
    };

    _mailRepository
      .GetByPeriodFromFolder(folderName, period, cancellationToken)
      .Returns(storedMails);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new LlmGenerateResponse("2, 999, 2"),
        new LlmGenerateResponse("  final digest  "));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, period, cancellationToken);

    // Then.
    await _mailRepository.Received(1).GetByPeriodFromFolder(folderName, period, cancellationToken);
    await _llmClient.Received(2).Generate(Arg.Any<LlmGenerateRequest>(), cancellationToken);

    Assert.That(result.TotalFetched, Is.EqualTo(2));
    Assert.That(result.Selected, Is.EqualTo(1));
    Assert.That(result.Digest, Is.EqualTo("final digest"));

    Assert.That(requests, Has.Count.EqualTo(2));

    Assert.Multiple(() =>
    {
      Assert.That(requests[0].Model, Is.EqualTo("llama3.2:3b"));
      Assert.That(requests[0].Prompt, Does.Contain("1."));
      Assert.That(requests[0].Prompt, Does.Contain("2."));
      Assert.That(requests[1].Model, Is.EqualTo("qwen2.5:7b-instruct"));
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Product release"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Subject: Weekly news"));
      Assert.That(requests[1].Prompt, Does.Contain($"Body preview: {new string('a', 1500)}"));
      Assert.That(requests[1].Prompt, Does.Not.Contain(new string('a', 1501)));
    });
  }

  [Test]
  public async Task BuildInboxDigestAsync_FallsBackToSubjectKeywords_WhenClassifierResponseIsUnusable()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var period = _fixture.Create<TimeSpan>();

    var storedMails = new[]
    {
      CreateStoredMail(subject: "Вышла версия 1.2.3", markdownBody: "first"),
      CreateStoredMail(subject: "General update", markdownBody: "second"),
      CreateStoredMail(subject: "Service release announcement", markdownBody: "third")
    };

    _mailRepository
      .GetByPeriodFromFolder(folderName, period, Arg.Any<CancellationToken>())
      .Returns(storedMails);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), Arg.Any<CancellationToken>())
      .Returns(
        new LlmGenerateResponse("not a list of ids"),
        new LlmGenerateResponse("digest"));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, period);

    // Then.
    Assert.That(result.TotalFetched, Is.EqualTo(3));
    Assert.That(result.Selected, Is.EqualTo(2));
    Assert.That(requests, Has.Count.EqualTo(2));

    Assert.Multiple(() =>
    {
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Вышла версия 1.2.3"));
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Service release announcement"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Subject: General update"));
    });
  }

  [Test]
  public async Task BuildInboxDigestAsync_ClassifiesEmailsInBatches_WhenEmailCountExceedsClassifierBatchSize()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var period = _fixture.Create<TimeSpan>();
    var storedMails = Enumerable.Range(1, 51)
      .Select(index => CreateStoredMail(subject: $"Message {index}", markdownBody: $"body {index}"))
      .ToArray();

    _mailRepository
      .GetByPeriodFromFolder(folderName, period, Arg.Any<CancellationToken>())
      .Returns(storedMails);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), Arg.Any<CancellationToken>())
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse("51"),
        new LlmGenerateResponse("digest"));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, period);

    // Then.
    Assert.That(result.Selected, Is.EqualTo(2));
    Assert.That(requests, Has.Count.EqualTo(3));

    Assert.Multiple(() =>
    {
      Assert.That(requests[0].Prompt, Does.Contain("1."));
      Assert.That(requests[0].Prompt, Does.Contain("50."));
      Assert.That(requests[0].Prompt, Does.Not.Contain("51."));
      Assert.That(requests[1].Prompt, Does.Contain("51."));
      Assert.That(requests[2].Prompt, Does.Contain("Subject: Message 1"));
      Assert.That(requests[2].Prompt, Does.Contain("Subject: Message 51"));
    });
  }

  [Test]
  public async Task BuildInboxDigestAsync_GeneratesDigestInBatches_WhenSelectedEmailCountExceedsDigestBatchSize()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var period = _fixture.Create<TimeSpan>();
    var storedMails = Enumerable.Range(1, 6)
      .Select(index => CreateStoredMail(subject: $"Release {index}", markdownBody: $"body {index}"))
      .ToArray();

    _mailRepository
      .GetByPeriodFromFolder(folderName, period, Arg.Any<CancellationToken>())
      .Returns(storedMails);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), Arg.Any<CancellationToken>())
      .Returns(
        new LlmGenerateResponse("1,2,3,4,5,6"),
        new LlmGenerateResponse("partial digest 1"),
        new LlmGenerateResponse("partial digest 2"),
        new LlmGenerateResponse("final merged digest"));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, period);

    // Then.
    Assert.That(result.Digest, Is.EqualTo("final merged digest"));
    Assert.That(requests, Has.Count.EqualTo(4));

    Assert.Multiple(() =>
    {
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Release 1"));
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Release 5"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Subject: Release 6"));
      Assert.That(requests[2].Prompt, Does.Contain("Subject: Release 6"));
      Assert.That(requests[3].Prompt, Does.Contain("partial digest 1"));
      Assert.That(requests[3].Prompt, Does.Contain("partial digest 2"));
    });
  }

  [Test]
  public async Task BuildInboxDigestAsync_UsesProvidedCancellationToken_ForClassifierAndDigestRequests()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var period = _fixture.Create<TimeSpan>();
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    var storedMails = new[]
    {
      CreateStoredMail(subject: "Product release", markdownBody: "body")
    };

    _mailRepository
      .GetByPeriodFromFolder(folderName, period, cancellationToken)
      .Returns(storedMails);

    _llmClient
      .Generate(Arg.Any<LlmGenerateRequest>(), cancellationToken)
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse("digest"));

    // When.
    await _sut.BuildInboxDigestAsync(folderName, period, cancellationToken);

    // Then.
    await _llmClient.Received(2).Generate(Arg.Any<LlmGenerateRequest>(), cancellationToken);
  }

  [Test]
  public async Task BuildInboxDigestAsync_GeneratesDigestWithEmptySelection_WhenNoReleaseEmailsWereSelected()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var period = _fixture.Create<TimeSpan>();
    var storedMails = new[]
    {
      CreateStoredMail(subject: "General update", markdownBody: "body")
    };

    _mailRepository
      .GetByPeriodFromFolder(folderName, period, Arg.Any<CancellationToken>())
      .Returns(storedMails);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), Arg.Any<CancellationToken>())
      .Returns(
        new LlmGenerateResponse(string.Empty),
        new LlmGenerateResponse("empty digest"));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, period);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.TotalFetched, Is.EqualTo(1));
      Assert.That(result.Selected, Is.EqualTo(0));
      Assert.That(result.Digest, Is.EqualTo("empty digest"));
      Assert.That(requests, Has.Count.EqualTo(2));
      Assert.That(requests[1].Prompt, Does.Contain("Письма:"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Subject:"));
    });
  }

  private StoredMail CreateStoredMail(
    string? subject = null,
    string? markdownBody = null)
  {
    return _fixture.Build<StoredMail>()
      .With(x => x.Id, 0)
      .With(x => x.Folder, _fixture.Create<string>())
      .With(x => x.MessageId, _fixture.Create<string>())
      .With(x => x.RawBody, _fixture.Create<string>())
      .With(x => x.Subject, subject ?? _fixture.Create<string>())
      .With(x => x.MarkdownBody, markdownBody ?? _fixture.Create<string>())
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
