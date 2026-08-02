using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class CommissionPricingCatalogTests
{
    private readonly Guid currentId = Guid.NewGuid();
    private readonly Guid previousId = Guid.NewGuid();

    [Fact]
    public void Current_ReturnsConfiguredCurrentRevision()
    {
        var catalog = BuildCatalog();

        Assert.Equal(new CommissionTerms(currentId, "2026.2", Currency.Gbp, 500), catalog.Current);
    }

    [Fact]
    public void GetRequired_ReturnsHistoricalRevision()
    {
        var catalog = BuildCatalog();

        Assert.Equal(new CommissionTerms(previousId, "2026.1", Currency.Gbp, 1000), catalog.GetRequired(previousId));
    }

    [Fact]
    public void GetRequired_UnconfiguredRevision_Throws()
    {
        var catalog = BuildCatalog();

        Assert.Throws<InvalidOperationException>(() => catalog.GetRequired(Guid.NewGuid()));
    }

    private CommissionPricingCatalog BuildCatalog() =>
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
