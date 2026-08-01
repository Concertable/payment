using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionConfigurationEntityTests
{
    [Fact]
    public void Create_OwnsImmutablePricingTerms()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var configuration = CommissionConfigurationEntity.Create(
            id, "2026.1", Currency.Gbp, 500, createdAt);

        Assert.Equal(id, configuration.Id);
        Assert.Equal("2026.1", configuration.Version);
        Assert.Equal(Currency.Gbp, configuration.Currency);
        Assert.Equal(500, configuration.RateBasisPoints);
        Assert.Equal(createdAt, configuration.CreatedAt);
        Assert.Equal(configuration.Terms, new CommissionTerms(id, "2026.1", Currency.Gbp, 500));
    }
}
