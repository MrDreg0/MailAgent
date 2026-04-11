using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Web.Browse;
using NSubstitute;

namespace MailAgent.Web.Tests;

[TestFixture]
public class MailBrowserServiceTests
{
  private IMailRepository _mailRepository = null!;
  private MailBrowserService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _mailRepository = Substitute.For<IMailRepository>();
    _sut = new MailBrowserService(_mailRepository);
  }

  [Test]
  public void GetPage_Throws_WhenPageNumberIsNotPositive()
  {
    // When.
    var act = () => _sut.GetPage(pageNumber: 0, pageSize: 10);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentOutOfRangeException>()
      .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pageNumber"));
  }

  [Test]
  public void GetPage_Throws_WhenPageSizeIsNotPositive()
  {
    // When.
    var act = () => _sut.GetPage(pageNumber: 1, pageSize: 0);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentOutOfRangeException>()
      .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pageSize"));
  }

  [Test]
  public async Task GetPage_ReturnsRequestedPage_AndPassesCalculatedSkipTakeAndCancellationToken()
  {
    // Given.
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    var expectedItems = new[]
    {
      CreateStoredMail(id: 21, subject: "Subject 21"),
      CreateStoredMail(id: 20, subject: "Subject 20")
    };

    _mailRepository
      .GetCount(cancellationToken)
      .Returns(42);

    _mailRepository
      .GetPage(skip: 20, take: 10, cancellationToken)
      .Returns(expectedItems);

    // When.
    var result = await _sut.GetPage(pageNumber: 3, pageSize: 10, cancellationToken);

    // Then.
    await _mailRepository.Received(1).GetCount(cancellationToken);
    await _mailRepository.Received(1).GetPage(skip: 20, take: 10, cancellationToken);

    Assert.Multiple(() =>
    {
      Assert.That(result.Items, Is.EqualTo(expectedItems));
      Assert.That(result.TotalCount, Is.EqualTo(42));
      Assert.That(result.PageNumber, Is.EqualTo(3));
      Assert.That(result.PageSize, Is.EqualTo(10));
    });
  }

  [Test]
  public async Task GetPage_ReturnsEmptyItems_WhenRepositoryReturnsEmptyPage()
  {
    // Given.
    _mailRepository
      .GetCount(Arg.Any<CancellationToken>())
      .Returns(0);

    _mailRepository
      .GetPage(skip: 0, take: 25, Arg.Any<CancellationToken>())
      .Returns(Array.Empty<StoredMail>());

    // When.
    var result = await _sut.GetPage(pageNumber: 1, pageSize: 25);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Items, Is.Empty);
      Assert.That(result.TotalCount, Is.EqualTo(0));
      Assert.That(result.PageNumber, Is.EqualTo(1));
      Assert.That(result.PageSize, Is.EqualTo(25));
    });
  }

  private static StoredMail CreateStoredMail(int id, string subject)
  {
    return new StoredMail(
      Id: id,
      Folder: "Releases",
      MessageId: $"message-{id}",
      DateUtc: DateTimeOffset.Parse("2026-03-16T10:00:00Z").AddMinutes(id),
      From: $"user{id}@example.com",
      Subject: subject,
      RawBody: $"raw-{id}",
      MarkdownBody: $"md-{id}",
      InsertedAt: "2026-03-16 10:00:00Z");
  }
}
