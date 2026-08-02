using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class CommissionTermsProviderTests
{
    private readonly Guid currentId = Guid.NewGuid();
    private readonly Guid previousId = Guid.NewGuid();

    [Fact]
    public void Current_ReturnsConfiguredCurrentRevision()
    {
        var provider = BuildProvider();

        Assert.Equal(new CommissionTerms(currentId, "2026.2", Currency.Gbp, 500), provider.Current);
    }

    [Fact]
    public void GetRequired_ReturnsHistoricalRevision()
    {
        var provider = BuildProvider();

        Assert.Equal(new CommissionTerms(previousId, "2026.1", Currency.Gbp, 1000), provider.GetRequired(previousId));
    }

    [Fact]
    public void GetRequired_UnconfiguredRevision_Throws()
    {
        var provider = BuildProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequired(Guid.NewGuid()));
    }

    private CommissionTermsProvider BuildProvider() =>
        new(Options.Create(new PlatformCommissionOptions
        {
            CurrentConfigurationId = currentId,
            Configurations =
            [
                new PlatformCommissionRevisionOptions
                {
                    Id = previousId,
                    Version = "2026.1",
                    Currency = "GBP",
                    RateBasisPoints = 1000
                },
                new PlatformCommissionRevisionOptions
                {
                    Id = currentId,
                    Version = "2026.2",
                    Currency = "GBP",
                    RateBasisPoints = 500
                }
            ]
        }));
}
