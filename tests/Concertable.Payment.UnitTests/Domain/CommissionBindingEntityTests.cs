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
            "order:42",
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
            "order:42",
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
            "order:42",
            "payer:7",
            DateTimeOffset.UtcNow);

        Assert.Equal(configuration.Id, binding.CommissionConfigurationId);
        Assert.Same(configuration, binding.CommissionConfiguration);
        Assert.Equal(configuration.Terms, binding.Terms);
        Assert.Equal(Currency.Gbp, binding.Currency);
    }

    [Fact]
    public void ConfirmReviewedGross_PersistsMoneyAndAllowsSameValue()
    {
        var binding = CommissionBindingEntity.Create(
            Configuration(), Currency.Gbp, "order:42", "payer:7", DateTimeOffset.UtcNow);
        var reviewedGross = Money.Gbp(50);

        binding.ConfirmReviewedGross(reviewedGross);
        binding.ConfirmReviewedGross(reviewedGross);

        Assert.Equal(reviewedGross, binding.ReviewedGross);
    }

    [Fact]
    public void ConfirmReviewedGross_RejectsDifferentAmount()
    {
        var binding = CommissionBindingEntity.Create(
            Configuration(), Currency.Gbp, "order:42", "payer:7", DateTimeOffset.UtcNow);
        binding.ConfirmReviewedGross(Money.Gbp(50));

        Assert.Throws<DomainException>(() => binding.ConfirmReviewedGross(Money.Gbp(51)));
    }

    [Fact]
    public void ConfirmReviewedGross_RejectsDifferentCurrency()
    {
        var binding = CommissionBindingEntity.Create(
            Configuration(), Currency.Gbp, "order:42", "payer:7", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() =>
            binding.ConfirmReviewedGross(new Money(50, (Currency)840)));
    }
}
