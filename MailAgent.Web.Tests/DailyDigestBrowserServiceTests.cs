using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Digest.Models;
using MailAgent.Web.Browse;
using NSubstitute;

namespace MailAgent.Web.Tests;

[TestFixture]
public class DailyDigestBrowserServiceTests
{
  private IDailyDigestRepository _dailyDigestRepository = null!;
  private DailyDigestBrowserService _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _dailyDigestRepository = Substitute.For<IDailyDigestRepository>();
    _sut = new DailyDigestBrowserService(_dailyDigestRepository);
  }

  [Test]
  public void GetPage_Throws_WhenFolderNameIsEmpty()
  {
    // When.
    var act = () => _sut.GetPage(string.Empty, pageNumber: 1, pageSize: 10);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentException>()
      .With.Property(nameof(ArgumentException.ParamName)).EqualTo("folderName"));
  }

  [Test]
  public void GetPage_Throws_WhenPageNumberIsNotPositive()
  {
    // When.
    var act = () => _sut.GetPage("Releases", pageNumber: 0, pageSize: 10);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentOutOfRangeException>()
      .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pageNumber"));
  }

  [Test]
  public void GetPage_Throws_WhenPageSizeIsNotPositive()
  {
    // When.
    var act = () => _sut.GetPage("Releases", pageNumber: 1, pageSize: 0);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentOutOfRangeException>()
      .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("pageSize"));
  }

  [Test]
  public async Task GetPage_ReturnsRequestedPage_AndPassesFolderSkipTakeAndCancellationToken()
  {
    // Given.
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    var expectedItems = new[]
    {
      CreateDailyDigest(id: 4, digestDate: new DateOnly(2026, 4, 10)),
      CreateDailyDigest(id: 3, digestDate: new DateOnly(2026, 4, 9))
    };

    _dailyDigestRepository
      .GetCount("Releases", cancellationToken)
      .Returns(11);

    _dailyDigestRepository
      .GetPage("Releases", skip: 2, take: 2, cancellationToken)
      .Returns(expectedItems);

    // When.
    var result = await _sut.GetPage("Releases", pageNumber: 2, pageSize: 2, cancellationToken);

    // Then.
    await _dailyDigestRepository.Received(1).GetCount("Releases", cancellationToken);
    await _dailyDigestRepository.Received(1).GetPage("Releases", skip: 2, take: 2, cancellationToken);

    Assert.Multiple(() =>
    {
      Assert.That(result.Items, Is.EqualTo(expectedItems));
      Assert.That(result.TotalCount, Is.EqualTo(11));
      Assert.That(result.PageNumber, Is.EqualTo(2));
      Assert.That(result.PageSize, Is.EqualTo(2));
    });
  }

  private static DailyDigest CreateDailyDigest(int id, DateOnly digestDate)
  {
    return new DailyDigest(
      Id: id,
      Folder: "Releases",
      DigestDate: digestDate,
      TotalFetched: 5,
      Selected: 2,
      DigestMarkdown: "# Digest",
      GeneratedAtUtc: DateTimeOffset.Parse("2026-04-12T08:00:00Z"));
  }
}
