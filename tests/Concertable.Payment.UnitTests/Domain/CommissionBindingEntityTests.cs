using Concertable.Kernel;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionBindingEntityTests
{
    [Fact]
    public void BindPaymentIntent_PreservesSetupIntentContext()
    {
        var binding = CommissionBindingEntity.Create(
            Guid.NewGuid(),
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripeSetupIntentId: "seti_123");

        binding.BindPaymentIntent("pi_123");

        Assert.Equal("seti_123", binding.StripeSetupIntentId);
        Assert.Equal("pi_123", binding.StripePaymentIntentId);
    }

    [Fact]
    public void BindPaymentIntent_RejectsDifferentIntent()
    {
        var binding = CommissionBindingEntity.Create(
            Guid.NewGuid(),
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripePaymentIntentId: "pi_123");

        Assert.Throws<DomainException>(() => binding.BindPaymentIntent("pi_other"));
    }

    [Fact]
    public void Entity_DoesNotDuplicateConfigurationTerms()
    {
        var properties = typeof(CommissionBindingEntity).GetProperties();

        Assert.DoesNotContain(properties, property => property.Name is "Version" or "Currency" or "RateBasisPoints");
        Assert.Contains(properties, property => property.Name == "CommissionConfigurationId");
    }
}
