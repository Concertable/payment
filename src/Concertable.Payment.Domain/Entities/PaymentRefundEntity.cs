namespace Concertable.Payment.Domain.Entities;

public sealed class PaymentRefundEntity : IGuidEntity
{
    private PaymentRefundEntity() { }

    private PaymentRefundEntity(
        int escrowId,
        string stripeRefundId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset completedAt)
    {
        if (string.IsNullOrWhiteSpace(stripeRefundId))
            throw new DomainException("Stripe refund id is required.");
        if (grossRefundedMinor <= 0)
            throw new DomainException("Gross refund must be positive.");
        if (commissionRefundedMinor < 0)
            throw new DomainException("Commission refund cannot be negative.");
        if (commissionVatReversedMinor < 0 || commissionVatReversedMinor > commissionRefundedMinor)
            throw new DomainException("Commission VAT reversal is invalid.");

        Id = Guid.NewGuid();
        EscrowId = escrowId;
        StripeRefundId = stripeRefundId;
        GrossRefundedMinor = grossRefundedMinor;
        CommissionRefundedMinor = commissionRefundedMinor;
        CommissionVatReversedMinor = commissionVatReversedMinor;
        PayerTotalRefundedMinor = checked(grossRefundedMinor + commissionRefundedMinor);
        Status = PaymentRefundStatus.Completed;
        CreatedAt = completedAt;
        CompletedAt = completedAt;
    }

    public Guid Id { get; private set; }
    public int EscrowId { get; private set; }
    public EscrowEntity Escrow { get; private set; } = null!;
    public string StripeRefundId { get; private set; } = null!;
    public long GrossRefundedMinor { get; private set; }
    public long CommissionRefundedMinor { get; private set; }
    public long CommissionVatReversedMinor { get; private set; }
    public long PayerTotalRefundedMinor { get; private set; }
    public PaymentRefundStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }

    public static PaymentRefundEntity CreateCompleted(
        int escrowId,
        string stripeRefundId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset completedAt) =>
        new(
            escrowId,
            stripeRefundId,
            grossRefundedMinor,
            commissionRefundedMinor,
            commissionVatReversedMinor,
            completedAt);
}
