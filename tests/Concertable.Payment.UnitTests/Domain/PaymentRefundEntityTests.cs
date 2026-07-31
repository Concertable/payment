using Concertable.Kernel;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentRefundEntityTests
{
    [Fact]
    public void CreatePendingForEscrow_StartsPendingWithoutStripeId()
    {
        var refund = PaymentRefundEntity.CreatePendingForEscrow(
            escrowId: 1,
            grossRefundedMinor: 3000,
            commissionRefundedMinor: 600,
            commissionVatReversedMinor: 100,
            DateTimeOffset.UtcNow);

        Assert.Equal(PaymentRefundStatus.Pending, refund.Status);
        Assert.Null(refund.StripeRefundId);
        Assert.Null(refund.CompletedAt);
        Assert.Equal(3600, refund.PayerTotalRefundedMinor);
        Assert.True(refund.CountsTowardCumulative);
        Assert.Equal(1, refund.EscrowId);
        Assert.Null(refund.SettlementTransactionId);
    }

    [Fact]
    public void CreatePendingForSettlement_StartsPendingWithoutStripeId()
    {
        var refund = PaymentRefundEntity.CreatePendingForSettlement(
            settlementTransactionId: 5,
            grossRefundedMinor: 2000,
            commissionRefundedMinor: 0,
            commissionVatReversedMinor: 0,
            DateTimeOffset.UtcNow);

        Assert.Equal(PaymentRefundStatus.Pending, refund.Status);
        Assert.Null(refund.StripeRefundId);
        Assert.Equal(5, refund.SettlementTransactionId);
        Assert.Null(refund.EscrowId);
    }

    [Fact]
    public void Complete_FromPending_SetsStripeIdAndCompletedAt()
    {
        var refund = PaymentRefundEntity.CreatePendingForEscrow(1, 3000, 0, 0, DateTimeOffset.UtcNow);
        var completedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        refund.Complete("re_done", completedAt);

        Assert.Equal(PaymentRefundStatus.Completed, refund.Status);
        Assert.Equal("re_done", refund.StripeRefundId);
        Assert.Equal(completedAt, refund.CompletedAt);
        Assert.True(refund.CountsTowardCumulative);
    }

    [Fact]
    public void Complete_RequiresStripeRefundId()
    {
        var refund = PaymentRefundEntity.CreatePendingForEscrow(1, 3000, 0, 0, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => refund.Complete(" ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_Twice_Throws()
    {
        var refund = PaymentRefundEntity.CreatePendingForEscrow(1, 3000, 0, 0, DateTimeOffset.UtcNow);
        refund.Complete("re_done", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => refund.Complete("re_again", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Fail_FromPending_MarksFailedAndStopsCounting()
    {
        var refund = PaymentRefundEntity.CreatePendingForEscrow(1, 3000, 0, 0, DateTimeOffset.UtcNow);

        refund.Fail();

        Assert.Equal(PaymentRefundStatus.Failed, refund.Status);
        Assert.Null(refund.StripeRefundId);
        Assert.False(refund.CountsTowardCumulative);
    }

    [Fact]
    public void Fail_AfterComplete_Throws()
    {
        var refund = PaymentRefundEntity.CreatePendingForEscrow(1, 3000, 0, 0, DateTimeOffset.UtcNow);
        refund.Complete("re_done", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(refund.Fail);
    }

    [Fact]
    public void CreatePending_RejectsNonPositiveGross()
    {
        Assert.Throws<DomainException>(() =>
            PaymentRefundEntity.CreatePendingForEscrow(1, 0, 0, 0, DateTimeOffset.UtcNow));
    }
}
