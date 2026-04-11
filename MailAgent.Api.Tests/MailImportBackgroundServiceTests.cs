using MailAgent.Api.BackgroundServices;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Import;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MailAgent.Api.Tests;

[TestFixture]
public class MailImportBackgroundServiceTests
{
  [Test]
  public async Task StartAsync_DoesNotCreateScope_WhenBackgroundImportIsDisabled()
  {
    // Given.
    var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
    var settings = new MailImportBackgroundSettings(
      Enabled: false,
      RunOnStartup: null,
      Interval: null,
      InitialLookbackPeriod: null,
      OverlapPeriod: null,
      Folders: []);

    var sut = new MailImportBackgroundService(
      serviceScopeFactory,
      settings,
      NullLogger<MailImportBackgroundService>.Instance);

    // When.
    await sut.StartAsync(CancellationToken.None);
    await sut.StopAsync(CancellationToken.None);

    // Then.
    serviceScopeFactory.DidNotReceive().CreateScope();
  }

  [Test]
  public async Task StartAsync_UsesLatestStoredDateMinusOverlap_WhenFolderAlreadyHasImportedMail()
  {
    // Given.
    var mailClient = Substitute.For<IMailClient>();
    var mailRepository = Substitute.For<IMailRepository>();
    var settings = new MailImportBackgroundSettings(
      Enabled: true,
      RunOnStartup: true,
      Interval: TimeSpan.FromDays(1),
      InitialLookbackPeriod: TimeSpan.FromDays(7),
      OverlapPeriod: TimeSpan.FromMinutes(30),
      Folders: ["Releases"]);

    var latestDateUtc = DateTimeOffset.Parse("2026-03-16T10:00:00Z");
    var expectedFromUtc = latestDateUtc - settings.OverlapPeriod!.Value;
    var importTriggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    mailRepository
      .GetLatestDateUtcByFolder("Releases", Arg.Any<CancellationToken>())
      .Returns(latestDateUtc);

    mailClient
      .GetMessageIdentifiersFromFolderSince(
        "Releases",
        Arg.Do<DateTimeOffset>(fromUtc =>
        {
          if (fromUtc == expectedFromUtc)
          {
            importTriggered.TrySetResult();
          }
        }),
        Arg.Any<CancellationToken>())
      .Returns(Array.Empty<MailMessageIdentifier>());

    mailRepository
      .GetExistingMessageIds(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    using var serviceProvider = CreateServiceProvider(mailClient, mailRepository);
    var sut = new MailImportBackgroundService(
      serviceProvider.GetRequiredService<IServiceScopeFactory>(),
      settings,
      NullLogger<MailImportBackgroundService>.Instance);

    // When.
    await sut.StartAsync(CancellationToken.None);
    await importTriggered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    await sut.StopAsync(CancellationToken.None);

    // Then.
    await mailRepository.Received(1).GetLatestDateUtcByFolder("Releases", Arg.Any<CancellationToken>());
    await mailClient.Received(1).GetMessageIdentifiersFromFolderSince(
      "Releases",
      expectedFromUtc,
      Arg.Any<CancellationToken>());
  }

  [Test]
  public async Task StartAsync_UsesInitialLookbackPeriod_WhenFolderHasNoImportedMailYet()
  {
    // Given.
    var mailClient = Substitute.For<IMailClient>();
    var mailRepository = Substitute.For<IMailRepository>();
    var settings = new MailImportBackgroundSettings(
      Enabled: true,
      RunOnStartup: true,
      Interval: TimeSpan.FromDays(1),
      InitialLookbackPeriod: TimeSpan.FromHours(6),
      OverlapPeriod: TimeSpan.FromMinutes(30),
      Folders: ["Releases"]);

    DateTimeOffset? capturedFromUtc = null;
    var importTriggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var lowerBound = DateTimeOffset.UtcNow - settings.InitialLookbackPeriod!.Value - TimeSpan.FromSeconds(2);

    mailRepository
      .GetLatestDateUtcByFolder("Releases", Arg.Any<CancellationToken>())
      .Returns((DateTimeOffset?)null);

    mailClient
      .GetMessageIdentifiersFromFolderSince(
        "Releases",
        Arg.Do<DateTimeOffset>(fromUtc =>
        {
          capturedFromUtc = fromUtc;
          importTriggered.TrySetResult();
        }),
        Arg.Any<CancellationToken>())
      .Returns(Array.Empty<MailMessageIdentifier>());

    mailRepository
      .GetExistingMessageIds(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
      .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    using var serviceProvider = CreateServiceProvider(mailClient, mailRepository);
    var sut = new MailImportBackgroundService(
      serviceProvider.GetRequiredService<IServiceScopeFactory>(),
      settings,
      NullLogger<MailImportBackgroundService>.Instance);

    // When.
    await sut.StartAsync(CancellationToken.None);
    await importTriggered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    var upperBound = DateTimeOffset.UtcNow - settings.InitialLookbackPeriod.Value + TimeSpan.FromSeconds(2);
    await sut.StopAsync(CancellationToken.None);

    // Then.
    Assert.That(capturedFromUtc, Is.Not.Null);
    Assert.That(capturedFromUtc, Is.InRange(lowerBound, upperBound));
  }

  private static ServiceProvider CreateServiceProvider(IMailClient mailClient, IMailRepository mailRepository)
  {
    var services = new ServiceCollection();
    services.AddSingleton(mailClient);
    services.AddSingleton(mailRepository);
    services.AddSingleton<EmailBodyConverter>();
    services.AddScoped<MailImportService>();

    return services.BuildServiceProvider();
  }
}
