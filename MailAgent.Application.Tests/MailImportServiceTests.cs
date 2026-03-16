using AutoFixture;
using NSubstitute;

namespace MailAgent.Application.Tests;

[TestFixture]
public class MailImportServiceTests
{
  private Fixture _fixture = null!;
  private IMailClient _mailClient = null!;
  private IMailRepository _mailRepository = null!;
  private EmailBodyConverter _bodyConverter = null!;
  private MailImportService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _mailClient = Substitute.For<IMailClient>();
    _mailRepository = Substitute.For<IMailRepository>();
    _bodyConverter = new EmailBodyConverter();
    _sut = new MailImportService(_bodyConverter, _mailClient, _mailRepository);
  }

  [Test]
  public void MapToStoredMail_ReturnsStoredMail_WhenMessageIdIsPresent()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var markdownBody = _fixture.Create<string>();
    var insertedAtUtc = _fixture.Create<DateTimeOffset>();
    var message = CreateMailMessage(messageId: "  message-id  ");

    // When.
    var storedMail = MailImportService.MapToStoredMail(folderName, message, markdownBody, insertedAtUtc);

    // Then.
    Assert.That(storedMail, Is.Not.Null);
    Assert.Multiple(() =>
    {
      Assert.That(storedMail!.Id, Is.EqualTo(0));
      Assert.That(storedMail.Folder, Is.EqualTo(folderName));
      Assert.That(storedMail.MessageId, Is.EqualTo("message-id"));
      Assert.That(storedMail.Subject, Is.EqualTo(message.Subject));
      Assert.That(storedMail.From, Is.EqualTo(message.From));
      Assert.That(storedMail.DateUtc, Is.EqualTo(message.DateUtc.ToUniversalTime()));
      Assert.That(storedMail.RawBody, Is.EqualTo(message.RawBody));
      Assert.That(storedMail.MarkdownBody, Is.EqualTo(markdownBody));
      Assert.That(storedMail.InsertedAt, Is.EqualTo(insertedAtUtc.ToString("u")));
    });
  }

  [Test]
  public void MapToStoredMail_ReturnsNull_WhenMessageIdIsWhiteSpace()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var markdownBody = _fixture.Create<string>();
    var insertedAtUtc = _fixture.Create<DateTimeOffset>();
    var message = CreateMailMessage(messageId: "   ");

    // When.
    var storedMail = MailImportService.MapToStoredMail(folderName, message, markdownBody, insertedAtUtc);

    // Then.
    Assert.That(storedMail, Is.Null);
  }

  [Test]
  public async Task ImportLatestFromFolderAsync_ReturnsSummariesAndSavesOnlyMessagesWithNonEmptyMessageId()
  {
    // Given.
    var folderName = _fixture.Create<string>();
    var takeCount = _fixture.Create<int>();
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    var persistedMessage = CreateMailMessage(
      messageId: "  persisted-message-id  ",
      htmlBody: "<p>Hello <strong>world</strong></p>",
      textBody: _fixture.Create<string>());

    var skippedMessage = CreateMailMessage(
      messageId: " ",
      htmlBody: null,
      textBody: "plain body");

    var fetchedMessages = new[] { persistedMessage, skippedMessage };

    _mailClient
      .GetLatestFromFolderAsync(folderName, takeCount, cancellationToken)
      .Returns(fetchedMessages);

    IReadOnlyCollection<StoredMail>? savedMails = null;
    CancellationToken savedCancellationToken = default;

    _mailRepository
      .SaveNewAsync(Arg.Do<IReadOnlyCollection<StoredMail>>(mails => savedMails = mails),
        Arg.Do<CancellationToken>(token => savedCancellationToken = token))
      .Returns(Task.CompletedTask);

    // When.
    var result = await _sut.ImportLatestFromFolderAsync(folderName, takeCount, cancellationToken);

    // Then.
    await _mailClient.Received(1).GetLatestFromFolderAsync(folderName, takeCount, cancellationToken);
    await _mailRepository.Received(1).SaveNewAsync(Arg.Any<IReadOnlyCollection<StoredMail>>(), cancellationToken);

    Assert.That(result.Total, Is.EqualTo(2));
    Assert.That(result.Latest, Has.Count.EqualTo(2));

    Assert.Multiple(() =>
    {
      Assert.That(result.Latest[0].ExternalId, Is.EqualTo(persistedMessage.ExternalId));
      Assert.That(result.Latest[0].MessageId, Is.EqualTo("persisted-message-id"));
      Assert.That(result.Latest[0].Subject, Is.EqualTo(persistedMessage.Subject));
      Assert.That(result.Latest[0].From, Is.EqualTo(persistedMessage.From));
      Assert.That(result.Latest[0].Date, Is.EqualTo(persistedMessage.DateUtc.ToString("u")));
      Assert.That(result.Latest[0].Body, Is.EqualTo("Hello **world**"));
      Assert.That(result.Latest[1].MessageId, Is.EqualTo(string.Empty));
      Assert.That(result.Latest[1].Body, Is.EqualTo("plain body"));
    });

    Assert.That(savedCancellationToken, Is.EqualTo(cancellationToken));
    Assert.That(savedMails, Is.Not.Null);
    Assert.That(savedMails, Has.Count.EqualTo(1));

    var storedMail = savedMails!.Single();
    Assert.Multiple(() =>
    {
      Assert.That(storedMail.Folder, Is.EqualTo(folderName));
      Assert.That(storedMail.MessageId, Is.EqualTo("persisted-message-id"));
      Assert.That(storedMail.Subject, Is.EqualTo(persistedMessage.Subject));
      Assert.That(storedMail.From, Is.EqualTo(persistedMessage.From));
      Assert.That(storedMail.DateUtc, Is.EqualTo(persistedMessage.DateUtc.ToUniversalTime()));
      Assert.That(storedMail.RawBody, Is.EqualTo(persistedMessage.RawBody));
      Assert.That(storedMail.MarkdownBody, Is.EqualTo("Hello **world**"));
    });
  }

  private MailMessage CreateMailMessage(
    string? messageId = null,
    string? htmlBody = null,
    string? textBody = null)
  {
    return _fixture.Build<MailMessage>()
      .With(x => x.MessageId, messageId ?? _fixture.Create<string>())
      .With(x => x.HtmlBody, htmlBody)
      .With(x => x.TextBody, textBody)
      .Create();
  }
}
