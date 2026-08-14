namespace Concertable.Payment.UnitTests.Domain;

public sealed class SettlementTransactionEntityTests
{
    private static SettlementTransactionEntity NewComplete()
    {
        var settlement = SettlementTransactionEntity.Create(
            payerId: Guid.NewGuid(),
            payeeId: Guid.NewGuid(),
            paymentIntentId: $"pi_{Guid.NewGuid():N}",
            amount: 6000,
            platformFee: 1000,
            status: TransactionStatus.Pending,
            bookingId: 42);
        settlement.Complete(DateTime.UtcNow);
        return settlement;
    }

    [Fact]
    public void Complete_FromPending_SetsImmutableCompletionTimestamp()
    {
        var settlement = SettlementTransactionEntity.Create(
            payerId: Guid.NewGuid(),
            payeeId: Guid.NewGuid(),
            paymentIntentId: $"pi_{Guid.NewGuid():N}",
            amount: 6000,
            platformFee: 1000,
            status: TransactionStatus.Pending,
            bookingId: 42);
        var completedAt = new DateTime(2026, 8, 14, 10, 30, 0, DateTimeKind.Utc);

        var first = settlement.Complete(completedAt);
        var second = settlement.Complete(completedAt.AddHours(1));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(completedAt, settlement.CompletedAt);
    }

    [Fact]
    public void RecordRefund_PartialGross_AddsReservationToRefunds()
    {
        var settlement = NewComplete();
        var refund = PaymentRefundEntity.CreateCompletedForSettlement(
            settlement.Id,
            "re_partial",
            grossRefundedMinor: 1000,
            commissionRefundedMinor: 0,
            commissionVatReversedMinor: 0,
            DateTimeOffset.UtcNow);

        settlement.RecordRefund(refund);

        Assert.Same(refund, Assert.Single(settlement.Refunds));
    }
}
