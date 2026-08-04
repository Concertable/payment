using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionBindingEntityTests
{
    private static CommissionConfigurationEntity Configuration() =>
        CommissionConfigurationEntity.Create(
            Guid.NewGuid(),
            Percentage.From(10m),
            DateTimeOffset.UtcNow);

    [Fact]
    public void BindPaymentIntent_PreservesSetupIntentContext()
    {
        var binding = CommissionBindingEntity.Create(
            Configuration(),
            Currency.Gbp,
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
            Configuration(),
            Currency.Gbp,
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow,
            stripePaymentIntentId: "pi_123");

        Assert.Throws<DomainException>(() => binding.BindPaymentIntent("pi_other"));
    }

    [Fact]
    public void Create_ReferencesConfigurationWithoutCopyingItsTerms()
    {
        var configuration = Configuration();

        var binding = CommissionBindingEntity.Create(
            configuration,
            Currency.Gbp,
            "booking:42",
            "payer:7",
            DateTimeOffset.UtcNow);

        Assert.Equal(configuration.Id, binding.CommissionConfigurationId);
        Assert.Same(configuration, binding.CommissionConfiguration);
        Assert.Equal(configuration.Terms, binding.Terms);
        Assert.Equal(Currency.Gbp, binding.Currency);
    }
}
