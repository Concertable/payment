using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionBindingEntityTests
{
    private static Guid ConfigurationId() => Guid.NewGuid();

    [Fact]
    public void BindPaymentIntent_PreservesSetupIntentContext()
    {
        var binding = CommissionBindingEntity.Create(
            ConfigurationId(),
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
            ConfigurationId(),
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripePaymentIntentId: "pi_123");

        Assert.Throws<DomainException>(() => binding.BindPaymentIntent("pi_other"));
    }

    [Fact]
    public void Create_ReferencesConfigurationWithoutCopyingItsTerms()
    {
        var configurationId = ConfigurationId();

        var binding = CommissionBindingEntity.Create(
            configurationId, "booking:42", "payer:7", DateTimeOffset.UtcNow);

        Assert.Equal(configurationId, binding.CommissionConfigurationId);
    }
}
