namespace Concertable.Payment.Infrastructure;

internal static class LedgerPostings
{
    public static LedgerPosting DirectSettlement(
        Guid payerId, Guid payeeId, Money gross, Money fee, int bookingId, string? paymentIntentId)
    {
        var legs = new List<PostingLeg>
        {
            new(new(LedgerAccountType.Receivable, payerId), LedgerDirection.Debit, gross + fee),
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Credit, gross)
        };
        if (fee.ToMinorUnits() > 0)
            legs.Add(new(new(LedgerAccountType.PlatformRevenue, null), LedgerDirection.Credit, fee));

        return new LedgerPosting(bookingId, paymentIntentId, legs);
    }

    public static LedgerPosting EscrowHold(
        Guid payerId, Money total, int bookingId, string? paymentIntentId) =>
        new(bookingId, paymentIntentId,
        [
            new(new(LedgerAccountType.Receivable, payerId), LedgerDirection.Debit, total),
            new(new(LedgerAccountType.StripeClearing, null), LedgerDirection.Credit, total)
        ]);

    public static LedgerPosting EscrowRelease(
        Guid payeeId, Money gross, Money fee, int bookingId, string? paymentIntentId)
    {
        var legs = new List<PostingLeg>
        {
            new(new(LedgerAccountType.StripeClearing, null), LedgerDirection.Debit, gross + fee),
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Credit, gross)
        };
        if (fee.ToMinorUnits() > 0)
            legs.Add(new(new(LedgerAccountType.PlatformRevenue, null), LedgerDirection.Credit, fee));

        return new LedgerPosting(bookingId, paymentIntentId, legs);
    }

    public static LedgerPosting EscrowRefundBeforeRelease(
        Guid payerId, Money refunded, int bookingId, string? paymentIntentId) =>
        new(bookingId, paymentIntentId,
        [
            new(new(LedgerAccountType.StripeClearing, null), LedgerDirection.Debit, refunded),
            new(new(LedgerAccountType.Receivable, payerId), LedgerDirection.Credit, refunded)
        ]);

    public static LedgerPosting EscrowRefundAfterRelease(
        Guid payerId, Guid payeeId, Money gross, Money fee, int bookingId, string? paymentIntentId)
    {
        var legs = new List<PostingLeg>
        {
            new(new(LedgerAccountType.Payable, payeeId), LedgerDirection.Debit, gross)
        };
        if (fee.ToMinorUnits() > 0)
            legs.Add(new(new(LedgerAccountType.PlatformRevenue, null), LedgerDirection.Debit, fee));
        legs.Add(new(new(LedgerAccountType.Receivable, payerId), LedgerDirection.Credit, gross + fee));

        return new LedgerPosting(bookingId, paymentIntentId, legs);
    }
}
