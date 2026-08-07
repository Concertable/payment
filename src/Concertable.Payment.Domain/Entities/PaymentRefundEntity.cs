namespace Concertable.Payment.Domain.Entities;

internal sealed class PaymentRefundEntity : IGuidEntity
{
    private PaymentRefundEntity() { }

    private PaymentRefundEntity(
        int? escrowId,
        int? settlementTransactionId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset createdAt)
    {
        if (escrowId is null == settlementTransactionId is null)
            throw new DomainException("A refund must belong to exactly one of an escrow or a settlement.");
        if (grossRefundedMinor <= 0)
            throw new DomainException("Gross refund must be positive.");
        if (commissionRefundedMinor < 0)
            throw new DomainException("Commission refund cannot be negative.");
        if (commissionVatReversedMinor < 0 || commissionVatReversedMinor > commissionRefundedMinor)
            throw new DomainException("Commission VAT reversal is invalid.");

        Id = Guid.NewGuid();
        EscrowId = escrowId;
        SettlementTransactionId = settlementTransactionId;
        GrossRefundedMinor = grossRefundedMinor;
        CommissionRefundedMinor = commissionRefundedMinor;
        CommissionVatReversedMinor = commissionVatReversedMinor;
        PayerTotalRefundedMinor = checked(grossRefundedMinor + commissionRefundedMinor);
        Status = PaymentRefundStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public int? EscrowId { get; private set; }
    public EscrowEntity? Escrow { get; private set; }
    public int? SettlementTransactionId { get; private set; }
    public SettlementTransactionEntity? SettlementTransaction { get; private set; }
    public string? StripeRefundId { get; private set; }
    public long GrossRefundedMinor { get; private set; }
    public long CommissionRefundedMinor { get; private set; }
    public long CommissionVatReversedMinor { get; private set; }
    public long PayerTotalRefundedMinor { get; private set; }
    public PaymentRefundStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool CountsTowardCumulative => Status != PaymentRefundStatus.Failed;

    public static PaymentRefundEntity CreatePendingForEscrow(
        int escrowId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset createdAt) =>
        new(
            escrowId,
            null,
            grossRefundedMinor,
            commissionRefundedMinor,
            commissionVatReversedMinor,
            createdAt);

    public static PaymentRefundEntity CreatePendingForSettlement(
        int settlementTransactionId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset createdAt) =>
        new(
            null,
            settlementTransactionId,
            grossRefundedMinor,
            commissionRefundedMinor,
            commissionVatReversedMinor,
            createdAt);

    public UnitResult<PaymentRefundTransitionError> Complete(
        string stripeRefundId,
        DateTimeOffset completedAt)
    {
        if (Status != PaymentRefundStatus.Pending)
            return UnitResult.Failure<PaymentRefundTransitionError>(new PaymentRefundTransitionError.NotPending(Status));

        if (string.IsNullOrWhiteSpace(stripeRefundId))
            throw new DomainException("Stripe refund id is required.");

        StripeRefundId = stripeRefundId;
        CompletedAt = completedAt;
        Status = PaymentRefundStatus.Completed;
        return UnitResult.Success<PaymentRefundTransitionError>();
    }

    public UnitResult<PaymentRefundTransitionError> Fail()
    {
        if (Status != PaymentRefundStatus.Pending)
            return UnitResult.Failure<PaymentRefundTransitionError>(new PaymentRefundTransitionError.NotPending(Status));

        Status = PaymentRefundStatus.Failed;
        return UnitResult.Success<PaymentRefundTransitionError>();
    }

    public static PaymentRefundEntity CreateCompletedForEscrow(
        int escrowId,
        string stripeRefundId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset completedAt)
    {
        var refund = CreatePendingForEscrow(
            escrowId,
            grossRefundedMinor,
            commissionRefundedMinor,
            commissionVatReversedMinor,
            completedAt);
        refund.Complete(stripeRefundId, completedAt);
        return refund;
    }

    public static PaymentRefundEntity CreateCompletedForSettlement(
        int settlementTransactionId,
        string stripeRefundId,
        long grossRefundedMinor,
        long commissionRefundedMinor,
        long commissionVatReversedMinor,
        DateTimeOffset completedAt)
    {
        var refund = CreatePendingForSettlement(
            settlementTransactionId,
            grossRefundedMinor,
            commissionRefundedMinor,
            commissionVatReversedMinor,
            completedAt);
        refund.Complete(stripeRefundId, completedAt);
        return refund;
    }
}
