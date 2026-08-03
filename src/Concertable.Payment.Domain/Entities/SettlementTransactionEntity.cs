namespace Concertable.Payment.Domain.Entities;

internal sealed class SettlementTransactionEntity : TransactionEntity
{
    private readonly List<PaymentRefundEntity> refunds = [];

    private SettlementTransactionEntity() { }

    private SettlementTransactionEntity(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        Currency currency,
        long payeeGrossMinor,
        long commissionGrossMinor,
        long commissionNetMinor,
        long commissionVatMinor,
        int commissionVatRateBasisPoints,
        TransactionStatus status,
        int bookingId,
        Guid? commissionBindingId)
        : base(
            payerId,
            payeeId,
            paymentIntentId,
            checked(payeeGrossMinor + commissionGrossMinor),
            status)
    {
        BookingId = bookingId;
        Currency = currency;
        PayeeGrossMinor = payeeGrossMinor;
        CommissionGrossMinor = commissionGrossMinor;
        CommissionNetMinor = commissionNetMinor;
        CommissionVatMinor = commissionVatMinor;
        CommissionVatRateBasisPoints = commissionVatRateBasisPoints;
        PayerTotalMinor = checked(payeeGrossMinor + commissionGrossMinor);
        CommissionBindingId = commissionBindingId;
    }

    public override TransactionType TransactionType => TransactionType.Settlement;
    public int BookingId { get; private set; }
    public Guid? CommissionBindingId { get; private set; }
    public CommissionBindingEntity? CommissionBinding { get; private set; }
    public Currency Currency { get; private set; }
    public long PayeeGrossMinor { get; private set; }
    public long CommissionGrossMinor { get; private set; }
    public long CommissionNetMinor { get; private set; }
    public long CommissionVatMinor { get; private set; }
    public int CommissionVatRateBasisPoints { get; private set; }
    public long PayerTotalMinor { get; private set; }

    /// <summary>
    /// Running total of cumulative gross reserved across non-failed refunds. Maintained by the
    /// repository's atomic conditional write (<c>ITransactionRepository.TryReserveSettlementRefundGrossAsync</c>),
    /// never by domain code — it is the concurrency guard that keeps cumulative gross refunds within
    /// <see cref="PayeeGrossMinor"/> under concurrent reservations.
    /// </summary>
    public long RefundedGrossMinor { get; private set; }
    public IReadOnlyCollection<PaymentRefundEntity> Refunds => refunds;

    public void RecordRefund(PaymentRefundEntity refund)
    {
        if (Status != TransactionStatus.Complete)
            throw new DomainException("Only a completed settlement can be refunded.");
        if (refund.SettlementTransactionId != Id)
            throw new DomainException("Refund belongs to another settlement.");

        refunds.Add(refund);
    }

    public void CompleteRefund(PaymentRefundEntity refund, string stripeRefundId, DateTimeOffset completedAt)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this settlement.");

        refund.Complete(stripeRefundId, completedAt);
    }

    public void ReleaseRefund(PaymentRefundEntity refund)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this settlement.");

        refund.Fail();
    }

    public static SettlementTransactionEntity Create(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        long amount,
        long platformFee,
        TransactionStatus status,
        int bookingId) =>
        new(
            payerId,
            payeeId,
            paymentIntentId,
            Currency.Gbp,
            checked(amount - platformFee),
            platformFee,
            platformFee,
            0,
            0,
            status,
            bookingId,
            null);

    internal static SettlementTransactionEntity CreateBound(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        CommissionCalculation calculation,
        TransactionStatus status,
        int bookingId,
        Guid commissionBindingId) =>
        new(
            payerId,
            payeeId,
            paymentIntentId,
            calculation.Currency,
            calculation.PayeeGrossMinor,
            calculation.CommissionGrossMinor,
            calculation.CommissionNetMinor,
            calculation.CommissionVatMinor,
            calculation.CommissionVatRateBasisPoints,
            status,
            bookingId,
            commissionBindingId);
}
