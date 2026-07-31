using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionBindingEntityTests
{
    private static CommissionTerms Terms() =>
        new(Guid.NewGuid(), "2024.1", Currency.Gbp, 1000, 2000);

    [Fact]
    public void BindPaymentIntent_PreservesSetupIntentContext()
    {
        var binding = CommissionBindingEntity.Create(
            Terms(),
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
            Terms(),
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripePaymentIntentId: "pi_123");

        Assert.Throws<DomainException>(() => binding.BindPaymentIntent("pi_other"));
    }

    [Fact]
    public void Create_SnapshotsConfigurationTermsOntoTheBinding()
    {
        var terms = Terms();

        var binding = CommissionBindingEntity.Create(
            terms, "booking:42", "payer:7", DateTimeOffset.UtcNow);

        Assert.Equal(terms.ConfigurationId, binding.CommissionConfigurationId);
        Assert.Equal(terms.Version, binding.Version);
        Assert.Equal(terms.Currency, binding.Currency);
        Assert.Equal(terms.RateBasisPoints, binding.RateBasisPoints);
        Assert.Equal(terms.VatRateBasisPoints, binding.VatRateBasisPoints);
        Assert.Equal(terms, binding.Terms);
    }
}
