namespace Concertable.Payment.Domain.Entities;

public sealed class CommissionAuthorizationEntity : IGuidEntity
{
    private CommissionAuthorizationEntity() { }

    private CommissionAuthorizationEntity(
        Guid id,
        Guid commissionConfigurationId,
        string externalReference,
        string payerReference,
        DateTimeOffset boundAt,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId)
    {
        if (id == Guid.Empty)
            throw new DomainException("Commission authorization id is required.");
        if (commissionConfigurationId == Guid.Empty)
            throw new DomainException("Commission configuration id is required.");
        if (string.IsNullOrWhiteSpace(externalReference))
            throw new DomainException("External reference is required.");
        if (string.IsNullOrWhiteSpace(payerReference))
            throw new DomainException("Payer reference is required.");
        Id = id;
        CommissionConfigurationId = commissionConfigurationId;
        ExternalReference = externalReference;
        PayerReference = payerReference;
        BoundAt = boundAt;
        StripePaymentIntentId = stripePaymentIntentId;
        StripeSetupIntentId = stripeSetupIntentId;
    }

    public Guid Id { get; private set; }
    public Guid CommissionConfigurationId { get; private set; }
    public CommissionConfigurationEntity CommissionConfiguration { get; private set; } = null!;
    public string ExternalReference { get; private set; } = null!;
    public string PayerReference { get; private set; } = null!;
    public DateTimeOffset BoundAt { get; private set; }
    public string? StripePaymentIntentId { get; private set; }
    public string? StripeSetupIntentId { get; private set; }

    public static CommissionAuthorizationEntity Create(
        Guid commissionConfigurationId,
        string externalReference,
        string payerReference,
        DateTimeOffset boundAt,
        string? stripePaymentIntentId = null,
        string? stripeSetupIntentId = null) =>
        new(
            Guid.NewGuid(),
            commissionConfigurationId,
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
            throw new DomainException("Commission authorization is already bound to another PaymentIntent.");

        StripePaymentIntentId = paymentIntentId;
    }

    public bool Matches(
        Guid commissionConfigurationId,
        string externalReference,
        string payerReference,
        string? stripePaymentIntentId,
        string? stripeSetupIntentId) =>
        CommissionConfigurationId == commissionConfigurationId &&
        string.Equals(ExternalReference, externalReference, StringComparison.Ordinal) &&
        string.Equals(PayerReference, payerReference, StringComparison.Ordinal) &&
        (stripePaymentIntentId is null ||
         string.Equals(StripePaymentIntentId, stripePaymentIntentId, StringComparison.Ordinal)) &&
        (stripeSetupIntentId is null ||
         string.Equals(StripeSetupIntentId, stripeSetupIntentId, StringComparison.Ordinal));
}
