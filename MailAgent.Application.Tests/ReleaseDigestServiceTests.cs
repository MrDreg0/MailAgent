using AutoFixture;
using MailAgent.Application.Ollama;
using NSubstitute;

namespace MailAgent.Application.Tests;

[TestFixture]
public class ReleaseDigestServiceTests
{
  private Fixture _fixture = null!;
  private IMailClient _mailClient = null!;
  private IOllamaClient _ollamaClient = null!;
  private ReleaseDigestService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _mailClient = Substitute.For<IMailClient>();
    _ollamaClient = Substitute.For<IOllamaClient>();
    _sut = new ReleaseDigestService(new EmailBodyConverter(), _mailClient, _ollamaClient);
  }

  [Test]
  public async Task BuildInboxDigestAsync_UsesClassifierIdsAndBuildsDigestFromSelectedEmails()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var takeCount = _fixture.Create<int>();
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    var selectedBody = new string('a', 1605);
    var fetchedMessages = new[]
    {
      CreateMailMessage(subject: "Weekly news", textBody: "noise"),
      CreateMailMessage(subject: "Product release", textBody: selectedBody)
    };

    _mailClient
      .GetLatestFromFolderAsync(folderName, takeCount, cancellationToken)
      .Returns(fetchedMessages);

    var requests = new List<OllamaGenerateRequest>();

    _ollamaClient
      .Generate(Arg.Do<OllamaGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new OllamaGenerateResponse("2, 999, 2"),
        new OllamaGenerateResponse("  final digest  "));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, takeCount, cancellationToken);

    // Then.
    await _mailClient.Received(1).GetLatestFromFolderAsync(folderName, takeCount, cancellationToken);
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
    var takeCount = _fixture.Create<int>();

    var fetchedMessages = new[]
    {
      CreateMailMessage(subject: "Вышла версия 1.2.3", textBody: "first"),
      CreateMailMessage(subject: "General update", textBody: "second"),
      CreateMailMessage(subject: "Service release announcement", textBody: "third")
    };

    _mailClient
      .GetLatestFromFolderAsync(folderName, takeCount, Arg.Any<CancellationToken>())
      .Returns(fetchedMessages);

    var requests = new List<OllamaGenerateRequest>();

    _ollamaClient
      .Generate(Arg.Do<OllamaGenerateRequest>(request => requests.Add(request)), Arg.Any<CancellationToken>())
      .Returns(
        new OllamaGenerateResponse("not a list of ids"),
        new OllamaGenerateResponse("digest"));

    // When.
    var result = await _sut.BuildInboxDigestAsync(folderName, takeCount);

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

  private MailMessage CreateMailMessage(
    string? subject = null,
    string? textBody = null,
    string? htmlBody = null)
  {
    return _fixture.Build<MailMessage>()
      .With(x => x.Subject, subject ?? _fixture.Create<string>())
      .With(x => x.TextBody, textBody)
      .With(x => x.HtmlBody, htmlBody)
      .Create();
  }
}
