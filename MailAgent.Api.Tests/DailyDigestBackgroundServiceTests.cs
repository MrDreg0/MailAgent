using MailAgent.Api.BackgroundServices;
using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Digest.Models;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Digest;
using MailAgent.Application.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MailAgent.Api.Tests;

[TestFixture]
public class DailyDigestBackgroundServiceTests
{
  [Test]
  public void GetDelayUntilNextCheck_ReturnsThresholdDelay_WhenThresholdIsSoonerThanInterval()
  {
    // Given.
    var utcNow = DateTimeOffset.Parse("2026-04-12T07:58:30Z");
    var interval = TimeSpan.FromDays(1);
    var generateAfter = new TimeOnly(8, 0, 0);

    // When.
    var result = DailyDigestBackgroundService.GetDelayUntilNextCheck(utcNow, interval, generateAfter);

    // Then.
    Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(90)));
  }

  [Test]
  public async Task StartAsync_DoesNotCreateScope_WhenBackgroundDigestIsDisabled()
  {
    // Given.
    var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
    var settings = new DailyDigestBackgroundSettings(
      Enabled: false,
      RunOnStartup: null,
      Interval: null,
      Folder: null,
      GenerateAfter: null);

    var sut = new DailyDigestBackgroundService(
      serviceScopeFactory,
      settings,
      NullLogger<DailyDigestBackgroundService>.Instance);

    // When.
    await sut.StartAsync(CancellationToken.None);
    await sut.StopAsync(CancellationToken.None);

    // Then.
    serviceScopeFactory.DidNotReceive().CreateScope();
  }

  [Test]
  public async Task StartAsync_GeneratesDigestForYesterday_WhenServiceRunsAfterConfiguredTime()
  {
    // Given.
    var digestRepository = Substitute.For<IDailyDigestRepository>();
    var mailRepository = Substitute.For<IMailRepository>();
    var llmClient = Substitute.For<ILlmClient>();

    digestRepository
      .GetByDate(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
      .Returns((DailyDigest?)null);

    mailRepository
      .GetByUtcRangeFromFolder(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
      .Returns(Array.Empty<MailAgent.Application.Contracts.Mail.Models.StoredMail>());

    var savedDigest = new TaskCompletionSource<DailyDigest>(TaskCreationOptions.RunContinuationsAsynchronously);

    digestRepository
      .When(repository => repository.Save(Arg.Any<DailyDigest>(), Arg.Any<CancellationToken>()))
      .Do(callInfo => savedDigest.TrySetResult(callInfo.Arg<DailyDigest>()));

    using var serviceProvider = CreateServiceProvider(digestRepository, mailRepository, llmClient);

    var sut = new DailyDigestBackgroundService(
      serviceProvider.GetRequiredService<IServiceScopeFactory>(),
      new DailyDigestBackgroundSettings(
        Enabled: true,
        RunOnStartup: true,
        Interval: TimeSpan.FromDays(1),
        Folder: "Releases",
        GenerateAfter: TimeOnly.MinValue),
      NullLogger<DailyDigestBackgroundService>.Instance);

    // When.
    await sut.StartAsync(CancellationToken.None);
    var result = await savedDigest.Task.WaitAsync(TimeSpan.FromSeconds(2));
    await sut.StopAsync(CancellationToken.None);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Folder, Is.EqualTo("Releases"));
      Assert.That(result.DigestDate, Is.EqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1))));
      Assert.That(result.DigestMarkdown, Does.Contain("No release mails were selected for this day."));
    });
  }

  private static ServiceProvider CreateServiceProvider(
    IDailyDigestRepository dailyDigestRepository,
    IMailRepository mailRepository,
    ILlmClient llmClient)
  {
    var services = new ServiceCollection();
    services.AddSingleton(dailyDigestRepository);
    services.AddSingleton(mailRepository);
    services.AddSingleton(llmClient);
    services.AddSingleton(new LlmSettings
    {
      Provider = LlmProvider.Ollama,
      BaseUrl = "http://localhost:11434/",
      Timeout = TimeSpan.FromMinutes(5),
      FastModel = "llama3.2:3b",
      MainModel = "qwen2.5:7b-instruct",
    });
    services.AddLogging();
    services.AddScoped<DailyDigestService>();
    services.AddScoped<DailyDigestGenerationService>();

    return services.BuildServiceProvider();
  }
}
