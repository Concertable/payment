using Concertable.Kernel;
using Concertable.Payment.Contracts.Enums;

namespace Concertable.Payment.Domain.Entities;

public sealed class EscrowEntity : IIdEntity, IAuditable
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
        int commissionVatRateBasisPoints,
        string chargeId,
        Guid? commissionAuthorizationId)
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
        CommissionVatRateBasisPoints = commissionVatRateBasisPoints;
        PayerTotalMinor = checked(payeeGrossMinor + commissionGrossMinor);
        ChargeId = chargeId;
        CommissionAuthorizationId = commissionAuthorizationId;
        Status = EscrowStatus.Pending;
        ConcurrencyToken = Guid.NewGuid();
    }

    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public Guid FromOwnerId { get; private set; }
    public Guid ToOwnerId { get; private set; }
    public Guid? CommissionAuthorizationId { get; private set; }
    public CommissionAuthorizationEntity? CommissionAuthorization { get; private set; }
    public Currency Currency { get; private set; }
    public long PayeeGrossMinor { get; private set; }
    public long CommissionGrossMinor { get; private set; }
    public long CommissionNetMinor { get; private set; }
    public long CommissionVatMinor { get; private set; }
    public int CommissionVatRateBasisPoints { get; private set; }
    public long PayerTotalMinor { get; private set; }
    public EscrowStatus Status { get; private set; }
    public string ChargeId { get; private set; } = null!;
    public string? TransferId { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public IReadOnlyCollection<PaymentRefundEntity> Refunds => refunds;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? LastModifiedAt { get; set; }
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
            0,
            chargeId,
            null);

    internal static EscrowEntity CreateAuthorized(
        int bookingId,
        Guid fromOwnerId,
        Guid toOwnerId,
        Guid commissionAuthorizationId,
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
            calculation.CommissionVatRateBasisPoints,
            chargeId,
            commissionAuthorizationId);

    public void Confirm()
    {
        if (Status != EscrowStatus.Pending)
            return;
        Status = EscrowStatus.Held;
    }

    public void Fail()
    {
        if (Status != EscrowStatus.Pending)
            return;
        Status = EscrowStatus.Failed;
    }

    public void Release(string transferId, DateTime now)
    {
        if (Status != EscrowStatus.Held)
            throw new DomainException("Only held escrow can be released.");
        TransferId = transferId;
        ReleasedAt = now;
        Status = EscrowStatus.Released;
    }

    public void RecordRefund(PaymentRefundEntity refund)
    {
        if (Status is not (EscrowStatus.Held or EscrowStatus.Released or EscrowStatus.Disputed))
            throw new DomainException("Only held, released, or disputed escrow can be refunded.");
        if (refund.EscrowId != Id)
            throw new DomainException("Refund belongs to another escrow.");

        refunds.Add(refund);
        // Bump the token so a reservation (which leaves Status unchanged) still forces the parent into the
        // optimistic-concurrency check; a child-only insert alone never updates the parent row, so two
        // concurrent reservations would not conflict at SaveChanges without this.
        ConcurrencyToken = Guid.NewGuid();
        SettleRefundedStatus();
    }

    public void CompleteRefund(PaymentRefundEntity refund, string stripeRefundId, DateTimeOffset completedAt)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this escrow.");

        refund.Complete(stripeRefundId, completedAt);
        ConcurrencyToken = Guid.NewGuid();
        SettleRefundedStatus();
    }

    public void ReleaseRefund(PaymentRefundEntity refund)
    {
        if (!refunds.Contains(refund))
            throw new DomainException("Refund does not belong to this escrow.");

        refund.Fail();
        ConcurrencyToken = Guid.NewGuid();
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

    public void MarkDisputed()
    {
        if (Status != EscrowStatus.Held)
            throw new DomainException("Only held escrow can be disputed.");
        Status = EscrowStatus.Disputed;
    }
}
