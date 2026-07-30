using Concertable.Kernel;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionAuthorizationEntityTests
{
    [Fact]
    public void BindPaymentIntent_PreservesSetupIntentContext()
    {
        var authorization = CommissionAuthorizationEntity.Create(
            Guid.NewGuid(),
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripeSetupIntentId: "seti_123");

        authorization.BindPaymentIntent("pi_123");

        Assert.Equal("seti_123", authorization.StripeSetupIntentId);
        Assert.Equal("pi_123", authorization.StripePaymentIntentId);
    }

    [Fact]
    public void BindPaymentIntent_RejectsDifferentIntent()
    {
        var authorization = CommissionAuthorizationEntity.Create(
            Guid.NewGuid(),
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripePaymentIntentId: "pi_123");

        Assert.Throws<DomainException>(() => authorization.BindPaymentIntent("pi_other"));
    }

    [Fact]
    public void Entity_DoesNotDuplicateConfigurationTerms()
    {
        var properties = typeof(CommissionAuthorizationEntity).GetProperties();

        Assert.DoesNotContain(properties, property => property.Name is "Version" or "Currency" or "RateBasisPoints");
        Assert.Contains(properties, property => property.Name == "CommissionConfigurationId");
    }
}
