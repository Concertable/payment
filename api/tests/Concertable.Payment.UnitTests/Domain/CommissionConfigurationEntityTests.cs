using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionConfigurationEntityTests
{
    [Fact]
    public void Create_OwnsImmutablePricingTerms()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var rate = Percentage.From(5m);

        var configuration = CommissionConfigurationEntity.Create(id, rate, createdAt);

        Assert.Equal(id, configuration.Id);
        Assert.Equal(rate, configuration.Rate);
        Assert.Equal(createdAt, configuration.CreatedAt);
        Assert.Equal(configuration.Terms, new CommissionTerms(id, rate));
    }
}
