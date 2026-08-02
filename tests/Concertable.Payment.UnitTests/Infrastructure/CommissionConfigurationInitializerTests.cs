using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class CommissionConfigurationInitializerTests
{
    private readonly Guid configurationId = Guid.NewGuid();
    private readonly Mock<ICommissionConfigurationRepository> repository = new();
    private readonly FakeTimeProvider timeProvider = new();

    [Fact]
    public async Task InitializeAsync_NoRevision_PersistsConfiguredTermsOnce()
    {
        CommissionConfigurationEntity? candidate = null;
        repository
            .Setup(r => r.GetOrCreateAsync(
                It.IsAny<CommissionConfigurationEntity>(),
                It.IsAny<CancellationToken>()))
            .Callback<CommissionConfigurationEntity, CancellationToken>((c, _) => candidate = c)
            .ReturnsAsync((CommissionConfigurationEntity c, CancellationToken _) => c);

        await BuildInitializer().InitializeAsync();

        Assert.NotNull(candidate);
        Assert.Equal(configurationId, candidate.Id);
        Assert.Equal(Percentage.From(5m), candidate.Rate);
        repository.Verify(
            r => r.GetOrCreateAsync(
                It.IsAny<CommissionConfigurationEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_IdentityResolvesToDifferentTerms_FailsStartup()
    {
        repository
            .Setup(r => r.GetOrCreateAsync(
                It.IsAny<CommissionConfigurationEntity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommissionConfigurationEntity.Create(
                configurationId,
                Percentage.From(7.5m),
                timeProvider.GetUtcNow()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuildInitializer().InitializeAsync());
    }

    private CommissionConfigurationInitializer BuildInitializer() =>
        new(
            repository.Object,
            Options.Create(new PlatformCommissionOptions
            {
                ConfigurationId = configurationId,
                RatePercentage = 5m
            }),
            timeProvider);
}
