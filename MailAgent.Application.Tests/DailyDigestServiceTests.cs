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
    _sut = new DailyDigestService(
      _mailRepository,
      _llmClient,
      CreateLlmSettings(),
      new DailyDigestSettings("Russian"),
      NullLogger<DailyDigestService>.Instance);
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
      Assert.That(requests[1].Prompt, Does.Contain("Write the final digest in Russian."));
      Assert.That(requests[1].Prompt, Does.Contain("Do not add source"));
      Assert.That(requests[1].Prompt, Does.Contain("Do not use emoji"));
      Assert.That(requests[1].Prompt, Does.Contain("no more than 5 sections"));
      Assert.That(requests[1].Prompt, Does.Contain("release notes"));
      Assert.That(requests[1].Prompt, Does.Contain("docker images"));
      Assert.That(requests[1].Prompt, Does.Contain("If there is a main product version and a separate installer email"));
      Assert.That(requests[1].Prompt, Does.Contain("Do not write phrases like 'the web client is available via the link'"));
      Assert.That(requests[1].Prompt, Does.Contain("If an email is mostly about links"));
      Assert.That(requests[1].Prompt, Does.Contain("If an email announces a new product version"));
      Assert.That(requests[1].Prompt, Does.Contain("what actually changed today?"));
    });
  }

  [Test]
  public async Task BuildForDate_UsesConfiguredLanguageForEmptyDigest_WhenNoReleaseEmailsWereSelected()
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
      .Returns(
        new LlmGenerateResponse(string.Empty),
        new LlmGenerateResponse("# Дайджест релизов за 2026-04-10\n\n## Главное\n- За этот день релизные письма не выбраны.\n\n## Релизы\n- Релизных записей нет."));

    // When.
    var result = await _sut.BuildForDate("Releases", digestDate);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.TotalFetched, Is.EqualTo(1));
      Assert.That(result.Selected, Is.EqualTo(0));
      Assert.That(result.DigestMarkdown, Does.Contain("За этот день релизные письма не выбраны."));
    });

    await _llmClient.Received(2).Generate(Arg.Any<LlmGenerateRequest>(), Arg.Any<CancellationToken>());
  }

  [Test]
  public async Task BuildForDate_PrefersVersionChangesSectionOverGeneralInfoNoise()
  {
    // Given.
    var digestDate = new DateOnly(2026, 4, 9);
    const string folderName = "Releases";
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    var noisyPrefix = string.Join(
      "\n",
      Enumerable.Repeat("Docker-образ example.internal/acme-proxy:2.3.7.0", 40));

    var markdownBody =
      $$"""
        # Общая информация

        {{noisyPrefix}}

        # Зависимости версии

        Identity Service: 2.4.28.21
        Message Broker: 2.5.10.0

        # Изменения версии

        ## Общие

        Исправлена работа с обновлением потока выпуска сертификата в БД.
        """;

    _mailRepository
      .GetByUtcRangeFromFolder(
        folderName,
        DateTimeOffset.Parse("2026-04-09T00:00:00Z"),
        DateTimeOffset.Parse("2026-04-10T00:00:00Z"),
        cancellationToken)
      .Returns([CreateStoredMail(subject: "Acme Proxy. Вышла версия 2.3.7.0", markdownBody: markdownBody)]);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse("# Release Digest for 2026-04-09\n\n## Highlights\n- Test"));

    // When.
    await _sut.BuildForDate(folderName, digestDate, cancellationToken);

    // Then.
    Assert.That(requests, Has.Count.EqualTo(2));
    Assert.Multiple(() =>
    {
      Assert.That(requests[1].Prompt, Does.Contain("# Изменения версии"));
      Assert.That(requests[1].Prompt, Does.Contain("Исправлена работа с обновлением потока выпуска сертификата в БД."));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Identity Service: 2.4.28.21"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Docker-образ example.internal/acme-proxy:2.3.7.0"));
    });
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
