using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class EscrowEntityTests
{
    private static EscrowEntity NewPending() =>
        EscrowEntity.Create(bookingId: 42, fromOwnerId: Guid.NewGuid(), toOwnerId: Guid.NewGuid(), gross: Money.Gbp(50), platformFee: Money.Gbp(0), chargeId: "pi_test");

    [Fact]
    public void Create_StartsInPending()
    {
        var escrow = NewPending();

        Assert.Equal(EscrowStatus.Pending, escrow.Status);
        Assert.Null(escrow.TransferId);
        Assert.Null(escrow.ReleasedAt);
        Assert.Empty(escrow.Refunds);
    }

    [Fact]
    public void Create_WithPlatformFee_SetsAmountToGrossPlusFee()
    {
        var escrow = EscrowEntity.Create(bookingId: 42, fromOwnerId: Guid.NewGuid(), toOwnerId: Guid.NewGuid(), gross: Money.Gbp(50), platformFee: Money.Gbp(12), chargeId: "pi_test");

        Assert.Equal(5000, escrow.PayeeGrossMinor);
        Assert.Equal(1200, escrow.CommissionGrossMinor);
        Assert.Equal(6200, escrow.PayerTotalMinor);
    }

    [Fact]
    public void Confirm_FromPending_TransitionsToHeld()
    {
        var escrow = NewPending();

        escrow.Confirm();

        Assert.Equal(EscrowStatus.Held, escrow.Status);
    }

    [Fact]
    public void Confirm_FromHeld_IsIdempotent()
    {
        var escrow = NewPending();
        escrow.Confirm();

        escrow.Confirm();

        Assert.Equal(EscrowStatus.Held, escrow.Status);
    }

    [Fact]
    public void Fail_FromPending_TransitionsToFailed()
    {
        var escrow = NewPending();

        escrow.Fail();

        Assert.Equal(EscrowStatus.Failed, escrow.Status);
    }

    [Fact]
    public void Fail_FromHeld_IsNoOp()
    {
        var escrow = NewPending();
        escrow.Confirm();

        escrow.Fail();

        Assert.Equal(EscrowStatus.Held, escrow.Status);
    }

    [Fact]
    public void Release_FromHeld_TransitionsToReleased()
    {
        var escrow = NewPending();
        escrow.Confirm();
        var now = DateTime.UtcNow;

        escrow.Release("tr_test", now);

        Assert.Equal(EscrowStatus.Released, escrow.Status);
        Assert.Equal("tr_test", escrow.TransferId);
        Assert.Equal(now, escrow.ReleasedAt);
    }

    [Fact]
    public void Release_FromPending_Throws()
    {
        var escrow = NewPending();

        Assert.Throws<DomainException>(() => escrow.Release("tr_test", DateTime.UtcNow));
    }

    [Fact]
    public void Release_FromReleased_Throws()
    {
        var escrow = NewPending();
        escrow.Confirm();
        escrow.Release("tr_test", DateTime.UtcNow);

        Assert.Throws<DomainException>(() => escrow.Release("tr_test_2", DateTime.UtcNow));
    }

    [Fact]
    public void RecordRefund_FromHeldWithFullGross_TransitionsToRefunded()
    {
        var escrow = NewPending();
        escrow.Confirm();
        var refund = FullRefund(escrow);

        escrow.RecordRefund(refund);

        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
        Assert.Same(refund, Assert.Single(escrow.Refunds));
    }

    [Fact]
    public void RecordRefund_FromHeldWithPartialGross_RemainsHeld()
    {
        var escrow = NewPending();
        escrow.Confirm();
        var refund = PaymentRefundEntity.CreateCompletedForEscrow(
            escrow.Id,
            "re_partial",
            grossRefundedMinor: 1000,
            commissionRefundedMinor: 0,
            commissionVatReversedMinor: 0,
            DateTimeOffset.UtcNow);

        escrow.RecordRefund(refund);

        Assert.Equal(EscrowStatus.Held, escrow.Status);
    }

    [Fact]
    public void RecordRefund_PartialGross_BumpsConcurrencyToken()
    {
        var escrow = NewPending();
        escrow.Confirm();
        var before = escrow.ConcurrencyToken;
        var refund = PaymentRefundEntity.CreateCompletedForEscrow(
            escrow.Id,
            "re_partial",
            grossRefundedMinor: 1000,
            commissionRefundedMinor: 0,
            commissionVatReversedMinor: 0,
            DateTimeOffset.UtcNow);

        escrow.RecordRefund(refund);

        Assert.NotEqual(before, escrow.ConcurrencyToken);
    }

    [Fact]
    public void RecordRefund_FromReleasedWithFullGross_TransitionsToRefunded()
    {
        var escrow = NewPending();
        escrow.Confirm();
        escrow.Release("tr_test", DateTime.UtcNow);

        escrow.RecordRefund(FullRefund(escrow));

        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
    }

    [Fact]
    public void RecordRefund_FromDisputedWithFullGross_TransitionsToRefunded()
    {
        var escrow = NewPending();
        escrow.Confirm();
        escrow.MarkDisputed();

        escrow.RecordRefund(FullRefund(escrow));

        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
    }

    [Fact]
    public void RecordRefund_FromPending_Throws()
    {
        var escrow = NewPending();

        Assert.Throws<DomainException>(() => escrow.RecordRefund(FullRefund(escrow)));
    }

    [Fact]
    public void RecordRefund_FromFailed_Throws()
    {
        var escrow = NewPending();
        escrow.Fail();

        Assert.Throws<DomainException>(() => escrow.RecordRefund(FullRefund(escrow)));
    }

    [Fact]
    public void MarkDisputed_FromHeld_TransitionsToDisputed()
    {
        var escrow = NewPending();
        escrow.Confirm();

        escrow.MarkDisputed();

        Assert.Equal(EscrowStatus.Disputed, escrow.Status);
    }

    [Fact]
    public void MarkDisputed_FromPending_Throws()
    {
        var escrow = NewPending();

        Assert.Throws<DomainException>(() => escrow.MarkDisputed());
    }

    private static PaymentRefundEntity FullRefund(EscrowEntity escrow) =>
        PaymentRefundEntity.CreateCompletedForEscrow(
            escrow.Id,
            $"re_{Guid.NewGuid():N}",
            escrow.PayeeGrossMinor,
            escrow.CommissionGrossMinor,
            escrow.CommissionVatMinor,
            DateTimeOffset.UtcNow);
}
