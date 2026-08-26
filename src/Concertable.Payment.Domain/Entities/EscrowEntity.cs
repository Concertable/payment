using Concertable.Kernel;
using Concertable.Payment.Contracts.Enums;

namespace Concertable.Payment.Domain.Entities;

internal sealed class EscrowEntity : IIdEntity, IAuditable
{
    private readonly List<PaymentRefundEntity> refunds = [];

    private EscrowEntity() { }

    private EscrowEntity(
        int bookingId,
        Guid fromOwnerId,
        Guid toOwnerId,
        Currency currency,
        long payeeGrossMinor,
        long commissionGrossMinor,
        long commissionNetMinor,
        long commissionVatMinor,
        Percentage commissionVatRate,
        string chargeId,
        Guid? commissionBindingId)
    {
        if (payeeGrossMinor < 0)
            throw new DomainException("Payee gross cannot be negative.");
        if (commissionGrossMinor < 0)
            throw new DomainException("Commission gross cannot be negative.");
        if (commissionNetMinor < 0 || commissionVatMinor < 0 ||
            checked(commissionNetMinor + commissionVatMinor) != commissionGrossMinor)
            throw new DomainException("Commission net and VAT must reconcile to commission gross.");

        BookingId = bookingId;
        FromOwnerId = fromOwnerId;
        ToOwnerId = toOwnerId;
        Currency = currency;
        PayeeGrossMinor = payeeGrossMinor;
        CommissionGrossMinor = commissionGrossMinor;
        CommissionNetMinor = commissionNetMinor;
        CommissionVatMinor = commissionVatMinor;
        CommissionVatRate = commissionVatRate;
        PayerTotalMinor = checked(payeeGrossMinor + commissionGrossMinor);
        ChargeId = chargeId;
        CommissionBindingId = commissionBindingId;
        Status = EscrowStatus.Pending;
    }

    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public Guid FromOwnerId { get; private set; }
    public Guid ToOwnerId { get; private set; }
    public Guid? CommissionBindingId { get; private set; }
    public CommissionBindingEntity? CommissionBinding { get; private set; }
    public Currency Currency { get; private set; }
    public long PayeeGrossMinor { get; private set; }
    public long CommissionGrossMinor { get; private set; }
    public long CommissionNetMinor { get; private set; }
    public long CommissionVatMinor { get; private set; }
    public Percentage CommissionVatRate { get; private set; }
    public long PayerTotalMinor { get; private set; }
    public EscrowStatus Status { get; private set; }
    public string ChargeId { get; private set; } = null!;
    public string? TransferId { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public Guid? ReleaseOperationId { get; private set; }
    public int? ReleaseOperationFingerprintVersion { get; private set; }
    public string? ReleaseOperationFingerprint { get; private set; }

    /// <summary>
    /// Running total of cumulative gross reserved across non-failed refunds. Maintained by the
    /// repository's atomic conditional write (<c>IEscrowRepository.TryReserveRefundGrossAsync</c>), never
    /// by domain code — it is the concurrency guard that keeps cumulative gross refunds within
    /// <see cref="PayeeGrossMinor"/> under concurrent reservations.
    /// </summary>
    public long RefundedGrossMinor { get; private set; }
    public IReadOnlyCollection<PaymentRefundEntity> Refunds => refunds;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }

    public static EscrowEntity Create(
        int bookingId,
        Guid fromOwnerId,
        Guid toOwnerId,
        Money gross,
        Money platformFee,
        string chargeId) =>
        new(
            bookingId,
            fromOwnerId,
            toOwnerId,
            gross.Currency,
            gross.ToMinorUnits(),
            platformFee.ToMinorUnits(),
            platformFee.ToMinorUnits(),
            0,
            Percentage.From(0m),
            chargeId,
            null);

    internal static EscrowEntity CreateBound(
        int bookingId,
        Guid fromOwnerId,
        Guid toOwnerId,
        Guid commissionBindingId,
        CommissionCalculation calculation,
        string chargeId) =>
        new(
            bookingId,
            fromOwnerId,
            toOwnerId,
            calculation.Currency,
            calculation.PayeeGrossMinor,
            calculation.CommissionGrossMinor,
            calculation.CommissionNetMinor,
            calculation.CommissionVatMinor,
            calculation.CommissionVatRate,
            chargeId,
            commissionBindingId);

    public UnitResult<EscrowTransitionError> Confirm()
    {
        if (Status != EscrowStatus.Pending)
            return UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.NotPending(Status));

        Status = EscrowStatus.Held;
        return UnitResult.Success<EscrowTransitionError>();
    }

    public UnitResult<EscrowTransitionError> Fail()
    {
        if (Status != EscrowStatus.Pending)
            return UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.NotPending(Status));

        Status = EscrowStatus.Failed;
        return UnitResult.Success<EscrowTransitionError>();
    }

    public UnitResult<EscrowTransitionError> Release(string transferId, DateTime now)
    {
        if (Status != EscrowStatus.Held)
            return UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.NotHeld(Status));

        TransferId = transferId;
        ReleasedAt = now;
        Status = EscrowStatus.Released;
        return UnitResult.Success<EscrowTransitionError>();
    }

    public UnitResult<EscrowTransitionError> BeginRelease(
        Guid operationId,
        SettlementOperationFingerprint fingerprint)
    {
        if (ReleaseOperationId is not null)
        {
            return ReleaseOperationId == operationId
                && ReleaseOperationFingerprintVersion == fingerprint.Version
                && string.Equals(ReleaseOperationFingerprint, fingerprint.Value, StringComparison.Ordinal)
                    ? UnitResult.Success<EscrowTransitionError>()
                    : UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.OperationConflict());
        }
        if (Status != EscrowStatus.Held)
            return UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.NotHeld(Status));

        ReleaseOperationId = operationId;
        ReleaseOperationFingerprintVersion = fingerprint.Version;
        ReleaseOperationFingerprint = fingerprint.Value;
        return UnitResult.Success<EscrowTransitionError>();
    }

    public UnitResult<EscrowTransitionError> RecordRefund(PaymentRefundEntity refund)
    {
        if (Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.NotRefundable(Status));

        if (refund.EscrowId != Id)
            throw new DomainException("Refund belongs to another escrow.");

        refunds.Add(refund);
        SettleRefundedStatus();
        return UnitResult.Success<EscrowTransitionError>();
    }

    public UnitResult<PaymentRefundTransitionError> CompleteRefund(
        PaymentRefundEntity refund,
        string stripeRefundId,
        DateTimeOffset completedAt)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this escrow.");

        var transition = refund.Complete(stripeRefundId, completedAt);
        if (transition.IsFailure)
            return transition;
        SettleRefundedStatus();
        return UnitResult.Success<PaymentRefundTransitionError>();
    }

    public UnitResult<PaymentRefundTransitionError> ReleaseRefund(PaymentRefundEntity refund)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this escrow.");

        var transition = refund.Fail();
        if (transition.IsFailure)
            return transition;
        return UnitResult.Success<PaymentRefundTransitionError>();
    }

    private void SettleRefundedStatus()
    {
        if (Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            return;
        var completedGross = refunds
            .Where(r => r.Status == PaymentRefundStatus.Completed)
            .Sum(r => r.GrossRefundedMinor);
        if (completedGross == PayeeGrossMinor)
            Status = EscrowStatus.Refunded;
    }

    public UnitResult<EscrowTransitionError> MarkDisputed()
    {
        if (Status != EscrowStatus.Held)
            return UnitResult.Failure<EscrowTransitionError>(new EscrowTransitionError.NotDisputable(Status));

        Status = EscrowStatus.Disputed;
        return UnitResult.Success<EscrowTransitionError>();
    }
}
