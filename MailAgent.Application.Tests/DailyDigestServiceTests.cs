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
      CreateStoredMail(subject: "Product release", markdownBody: new string('a', 6105))
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
        new LlmGenerateResponse("- Normalized release change"),
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
      Assert.That(requests, Has.Count.EqualTo(3));
      Assert.That(requests[1].Prompt, Does.Contain("Выдели только содержательные изменения"));
      Assert.That(requests[1].Prompt, Does.Contain("Subject: Product release"));
      Assert.That(requests[1].Prompt, Does.Not.Contain("Subject: General update"));
      Assert.That(requests[1].Prompt, Does.Contain($"Body preview:\n{new string('a', 6000)}"));
      Assert.That(requests[1].Prompt, Does.Not.Contain(new string('a', 6001)));
      Assert.That(requests[1].Prompt, Does.Contain("snake_case / CamelCase / test-style"));
      Assert.That(requests[1].Prompt, Does.Contain("Не оставляй голый технический идентификатор"));
      Assert.That(requests[2].Prompt, Does.Contain("Составь короткий утренний markdown-дайджест релизов"));
      Assert.That(requests[2].Prompt, Does.Contain("Не добавляй source"));
      Assert.That(requests[2].Prompt, Does.Contain("Не используй эмодзи"));
      Assert.That(requests[2].Prompt, Does.Contain("не больше 5 секций"));
      Assert.That(requests[2].Prompt, Does.Contain("release notes"));
      Assert.That(requests[2].Prompt, Does.Contain("docker-образы"));
      Assert.That(requests[2].Prompt, Does.Contain("Если есть основная версия продукта и отдельное письмо про installer"));
      Assert.That(requests[2].Prompt, Does.Contain("Не пиши фразы вроде \"веб-клиент доступен по ссылке\""));
      Assert.That(requests[2].Prompt, Does.Contain("Если письмо почти целиком про ссылку"));
      Assert.That(requests[2].Prompt, Does.Contain("Если письмо сообщает о новой версии продукта"));
      Assert.That(requests[2].Prompt, Does.Contain("snake_case / CamelCase / test-style"));
      Assert.That(requests[2].Prompt, Does.Contain("Даже если во входном normalized summary остались внутренние идентификаторы"));
      Assert.That(requests[2].Prompt, Does.Contain("что реально изменилось за день?"));
      Assert.That(requests[2].Prompt, Does.Contain("Normalized summary: - Normalized release change"));
      Assert.That(requests[2].Prompt, Does.Not.Contain($"Normalized summary: {new string('a', 6000)}"));
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
        new LlmGenerateResponse("- Исправлена работа с обновлением потока выпуска сертификата в БД."),
        new LlmGenerateResponse("# Release Digest for 2026-04-09\n\n## Highlights\n- Test"));

    // When.
    await _sut.BuildForDate(folderName, digestDate, cancellationToken);

    // Then.
    Assert.That(requests, Has.Count.EqualTo(3));
    Assert.Multiple(() =>
    {
      Assert.That(requests[1].Prompt, Does.Contain("Выдели только содержательные изменения"));
      Assert.That(requests[1].Prompt, Does.Contain("Body preview:"));
      Assert.That(requests[1].Prompt, Does.Contain("Docker-образ example.internal/acme-proxy:2.3.7.0"));
      Assert.That(requests[1].Prompt, Does.Contain("Исправлена работа с обновлением потока выпуска сертификата в БД."));
      Assert.That(requests[2].Prompt, Does.Contain("Normalized summary: - Исправлена работа с обновлением потока выпуска сертификата в БД."));
      Assert.That(requests[2].Prompt, Does.Not.Contain("Normalized summary: # Общая информация"));
    });
  }

  [Test]
  public async Task BuildForDate_UsesEnglishVersionChangesHeading_ForEnglishOutputLanguage()
  {
    // Given.
    var digestDate = new DateOnly(2026, 4, 9);
    const string folderName = "Releases";
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    var englishSut = new DailyDigestService(
      _mailRepository,
      _llmClient,
      CreateLlmSettings(),
      new DailyDigestSettings("English"),
      NullLogger<DailyDigestService>.Instance);

    var markdownBody =
      """
      # General Information

      Docker images and packages.

      # Version Changes

      Certificate issuance flow update in the database.
      """;

    _mailRepository
      .GetByUtcRangeFromFolder(
        folderName,
        DateTimeOffset.Parse("2026-04-09T00:00:00Z"),
        DateTimeOffset.Parse("2026-04-10T00:00:00Z"),
        cancellationToken)
      .Returns([CreateStoredMail(subject: "Acme Proxy 2.3.7.0", markdownBody: markdownBody)]);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse("- Certificate issuance flow update in the database."),
        new LlmGenerateResponse("# Release Digest for 2026-04-09\n\n## Highlights\n- Test"));

    // When.
    await englishSut.BuildForDate(folderName, digestDate, cancellationToken);

    // Then.
    Assert.That(requests, Has.Count.EqualTo(3));
    Assert.Multiple(() =>
    {
      Assert.That(requests[1].Prompt, Does.Contain("Extract only the meaningful changes"));
      Assert.That(requests[1].Prompt, Does.Contain("Body preview:"));
      Assert.That(requests[1].Prompt, Does.Contain("Docker images and packages."));
      Assert.That(requests[1].Prompt, Does.Contain("Certificate issuance flow update in the database."));
      Assert.That(requests[2].Prompt, Does.Contain("Normalized summary: - Certificate issuance flow update in the database."));
      Assert.That(requests[2].Prompt, Does.Not.Contain("Normalized summary: # General Information"));
    });
  }

  [Test]
  public async Task BuildForDate_FallsBackToOriginalPreview_WhenNormalizationReturnsEmptyText()
  {
    // Given.
    var digestDate = new DateOnly(2026, 4, 10);
    const string folderName = "Releases";
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    _mailRepository
      .GetByUtcRangeFromFolder(
        folderName,
        DateTimeOffset.Parse("2026-04-10T00:00:00Z"),
        DateTimeOffset.Parse("2026-04-11T00:00:00Z"),
        cancellationToken)
      .Returns([
        CreateStoredMail(subject: "Product release", markdownBody: "Meaningful body preview")
      ]);

    var requests = new List<LlmGenerateRequest>();

    _llmClient
      .Generate(Arg.Do<LlmGenerateRequest>(request => requests.Add(request)), cancellationToken)
      .Returns(
        new LlmGenerateResponse("1"),
        new LlmGenerateResponse("   "),
        new LlmGenerateResponse("# Release Digest for 2026-04-10\n\n## Highlights\n- Test"));

    // When.
    await _sut.BuildForDate(folderName, digestDate, cancellationToken);

    // Then.
    Assert.That(requests, Has.Count.EqualTo(3));
    Assert.That(requests[2].Prompt, Does.Contain("Normalized summary: Meaningful body preview"));
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
