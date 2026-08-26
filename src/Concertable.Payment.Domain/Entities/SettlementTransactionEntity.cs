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
        Percentage commissionVatRate,
        TransactionStatus status,
        int bookingId,
        Guid? commissionBindingId,
        Guid? operationId,
        SettlementOperationFingerprint? operationFingerprint,
        bool requiresAction,
        string? clientSecret)
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
        CommissionVatRate = commissionVatRate;
        PayerTotalMinor = checked(payeeGrossMinor + commissionGrossMinor);
        CommissionBindingId = commissionBindingId;
        OperationId = operationId;
        OperationFingerprintVersion = operationFingerprint?.Version;
        OperationFingerprint = operationFingerprint?.Value;
        RequiresAction = requiresAction;
        ClientSecret = clientSecret;
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
    public Percentage CommissionVatRate { get; private set; }
    public long PayerTotalMinor { get; private set; }
    public Guid? OperationId { get; private set; }
    public int? OperationFingerprintVersion { get; private set; }
    public string? OperationFingerprint { get; private set; }
    public bool RequiresAction { get; private set; }
    public string? ClientSecret { get; private set; }

    /// <summary>
    /// Running total of cumulative gross reserved across non-failed refunds. Maintained by the
    /// repository's atomic conditional write (<c>ITransactionRepository.TryReserveSettlementRefundGrossAsync</c>),
    /// never by domain code — it is the concurrency guard that keeps cumulative gross refunds within
    /// <see cref="PayeeGrossMinor"/> under concurrent reservations.
    /// </summary>
    public long RefundedGrossMinor { get; private set; }
    public IReadOnlyCollection<PaymentRefundEntity> Refunds => refunds;

    public UnitResult<TransactionTransitionError> RecordRefund(PaymentRefundEntity refund)
    {
        if (Status != TransactionStatus.Complete)
            return UnitResult.Failure<TransactionTransitionError>(new TransactionTransitionError.NotComplete(Status));

        if (refund.SettlementTransactionId != Id)
            throw new DomainException("Refund belongs to another settlement.");

        refunds.Add(refund);
        return UnitResult.Success<TransactionTransitionError>();
    }

    public UnitResult<PaymentRefundTransitionError> CompleteRefund(
        PaymentRefundEntity refund,
        string stripeRefundId,
        DateTimeOffset completedAt)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this settlement.");

        var transition = refund.Complete(stripeRefundId, completedAt);
        if (transition.IsFailure)
            return transition;
        return UnitResult.Success<PaymentRefundTransitionError>();
    }

    public UnitResult<PaymentRefundTransitionError> ReleaseRefund(PaymentRefundEntity refund)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this settlement.");

        var transition = refund.Fail();
        if (transition.IsFailure)
            return transition;
        return UnitResult.Success<PaymentRefundTransitionError>();
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
            Percentage.From(0m),
            status,
            bookingId,
            null,
            null,
            null,
            false,
            null);

    internal static SettlementTransactionEntity CreateForOperation(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        long amount,
        long platformFee,
        TransactionStatus status,
        int bookingId,
        Guid operationId,
        SettlementOperationFingerprint operationFingerprint,
        bool requiresAction,
        string? clientSecret) =>
        new(
            payerId,
            payeeId,
            paymentIntentId,
            Currency.Gbp,
            checked(amount - platformFee),
            platformFee,
            platformFee,
            0,
            Percentage.From(0m),
            status,
            bookingId,
            null,
            operationId,
            operationFingerprint,
            requiresAction,
            clientSecret);

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
            calculation.CommissionVatRate,
            status,
            bookingId,
            commissionBindingId,
            null,
            null,
            false,
            null);

    internal bool MatchesOperation(
        Guid operationId,
        SettlementOperationFingerprint fingerprint) =>
        OperationId == operationId
        && OperationFingerprintVersion == fingerprint.Version
        && string.Equals(OperationFingerprint, fingerprint.Value, StringComparison.Ordinal);
}
