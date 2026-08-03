namespace Concertable.Payment.Domain.Entities;

internal sealed class CommissionBindingEntity : IGuidEntity
{
    private CommissionBindingEntity() { }

    private CommissionBindingEntity(
        Guid id,
        CommissionConfigurationEntity commissionConfiguration,
        Currency currency,
        string externalReference,
        string payerReference,
        DateTimeOffset boundAt,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId)
    {
        if (id == Guid.Empty)
            throw new DomainException("Commission binding id is required.");
        ArgumentNullException.ThrowIfNull(commissionConfiguration);
        if (currency != Currency.Gbp)
            throw new DomainException("Commission currency must be GBP.");
        if (string.IsNullOrWhiteSpace(externalReference))
            throw new DomainException("External reference is required.");
        if (string.IsNullOrWhiteSpace(payerReference))
            throw new DomainException("Payer reference is required.");

        Id = id;
        CommissionConfigurationId = commissionConfiguration.Id;
        CommissionConfiguration = commissionConfiguration;
        Currency = currency;
        ExternalReference = externalReference;
        PayerReference = payerReference;
        BoundAt = boundAt;
        StripePaymentIntentId = stripePaymentIntentId;
        StripeSetupIntentId = stripeSetupIntentId;
    }

    public Guid Id { get; private set; }
    public Guid CommissionConfigurationId { get; private set; }
    public CommissionConfigurationEntity CommissionConfiguration { get; private set; } = null!;
    public Currency Currency { get; private set; }
    public string ExternalReference { get; private set; } = null!;
    public string PayerReference { get; private set; } = null!;
    public DateTimeOffset BoundAt { get; private set; }
    public string? StripePaymentIntentId { get; private set; }
    public string? StripeSetupIntentId { get; private set; }

    public CommissionTerms Terms => CommissionConfiguration.Terms;

    public static CommissionBindingEntity Create(
        CommissionConfigurationEntity commissionConfiguration,
        Currency currency,
        string externalReference,
        string payerReference,
        DateTimeOffset boundAt,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null) =>
        new(
            Guid.NewGuid(),
            commissionConfiguration,
            currency,
            externalReference,
            payerReference,
            boundAt,
            stripePaymentIntentId,
            stripeSetupIntentId);

    public void BindPaymentIntent(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new DomainException("Stripe PaymentIntent id is required.");
        if (StripePaymentIntentId is not null &&
            !string.Equals(StripePaymentIntentId, paymentIntentId, StringComparison.Ordinal))
            throw new DomainException("Commission binding is already bound to another PaymentIntent.");

        StripePaymentIntentId = paymentIntentId;
    }

    public bool Matches(
        Guid commissionConfigurationId,
        Currency currency,
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId) =>
        CommissionConfigurationId == commissionConfigurationId &&
        Currency == currency &&
        string.Equals(ExternalReference, externalReference, StringComparison.Ordinal) &&
        string.Equals(PayerReference, payerReference, StringComparison.Ordinal) &&
        (stripePaymentIntentId is null ||
         string.Equals(StripePaymentIntentId, stripePaymentIntentId, StringComparison.Ordinal)) &&
        (stripeSetupIntentId is null ||
         string.Equals(StripeSetupIntentId, stripeSetupIntentId, StringComparison.Ordinal));
}
