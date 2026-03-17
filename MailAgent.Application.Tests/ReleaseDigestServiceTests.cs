using AutoFixture;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Contracts.Ollama;
using MailAgent.Application.Digest;
using MailAgent.Application.Ollama;
using NSubstitute;

namespace MailAgent.Application.Tests;

[TestFixture]
public class ReleaseDigestServiceTests
{
  private Fixture _fixture = null!;
  private IMailRepository _mailRepository = null!;
  private IOllamaClient _ollamaClient = null!;
  private ReleaseDigestService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _mailRepository = Substitute.For<IMailRepository>();
    _ollamaClient = Substitute.For<IOllamaClient>();
    _sut = new ReleaseDigestService(_mailRepository, _ollamaClient);
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

    var requests = new List<OllamaGenerateRequest>();

    _ollamaClient
      .Generate(Arg.Do<OllamaGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new OllamaGenerateResponse("2, 999, 2"),
        new OllamaGenerateResponse("  final digest  "));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, period, cancellationToken);

    // Then.
    await _mailRepository.Received(1).GetByPeriodFromFolder(folderName, period, cancellationToken);
    await _ollamaClient.Received(2).Generate(Arg.Any<OllamaGenerateRequest>(), cancellationToken);

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

    var requests = new List<OllamaGenerateRequest>();

    _ollamaClient
      .Generate(Arg.Do<OllamaGenerateRequest>(request => requests.Add(request)), Arg.Any<CancellationToken>())
      .Returns(
        new OllamaGenerateResponse("not a list of ids"),
        new OllamaGenerateResponse("digest"));

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
}
