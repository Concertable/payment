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
        settlement.Complete();
        return settlement;
    }

    [Fact]
    public void RecordRefund_PartialGross_BumpsConcurrencyToken()
    {
        var settlement = NewComplete();
        var before = settlement.ConcurrencyToken;
        var refund = PaymentRefundEntity.CreateCompletedForSettlement(
            settlement.Id,
            "re_partial",
            grossRefundedMinor: 1000,
            commissionRefundedMinor: 0,
            commissionVatReversedMinor: 0,
            DateTimeOffset.UtcNow);

        settlement.RecordRefund(refund);

        Assert.NotEqual(before, settlement.ConcurrencyToken);
        Assert.Same(refund, Assert.Single(settlement.Refunds));
    }
}
