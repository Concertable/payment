using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class EscrowEntityTests
{
    private static EscrowEntity NewPending() =>
        EscrowEntity.Create(bookingId: 42, fromOwnerId: Guid.NewGuid(), toOwnerId: Guid.NewGuid(), amount: Money.Gbp(50), chargeId: "pi_test");

    [Fact]
    public void Create_StartsInPending()
    {
        var escrow = NewPending();

        Assert.Equal(EscrowStatus.Pending, escrow.Status);
        Assert.Null(escrow.TransferId);
        Assert.Null(escrow.RefundId);
        Assert.Null(escrow.ReleasedAt);
        Assert.Null(escrow.RefundedAt);
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
    public void Refund_FromHeld_TransitionsToRefunded()
    {
        var escrow = NewPending();
        escrow.Confirm();
        var now = DateTime.UtcNow;

        escrow.Refund("re_test", now);

        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
        Assert.Equal("re_test", escrow.RefundId);
        Assert.Equal(now, escrow.RefundedAt);
    }

    [Fact]
    public void Refund_FromReleased_TransitionsToRefunded()
    {
        var escrow = NewPending();
        escrow.Confirm();
        escrow.Release("tr_test", DateTime.UtcNow);

        escrow.Refund("re_test", DateTime.UtcNow);

        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
    }

    [Fact]
    public void Refund_FromDisputed_TransitionsToRefunded()
    {
        var escrow = NewPending();
        escrow.Confirm();
        escrow.MarkDisputed();

        escrow.Refund("re_test", DateTime.UtcNow);

        Assert.Equal(EscrowStatus.Refunded, escrow.Status);
    }

    [Fact]
    public void Refund_FromPending_Throws()
    {
        var escrow = NewPending();

        Assert.Throws<DomainException>(() => escrow.Refund("re_test", DateTime.UtcNow));
    }

    [Fact]
    public void Refund_FromFailed_Throws()
    {
        var escrow = NewPending();
        escrow.Fail();

        Assert.Throws<DomainException>(() => escrow.Refund("re_test", DateTime.UtcNow));
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
}
