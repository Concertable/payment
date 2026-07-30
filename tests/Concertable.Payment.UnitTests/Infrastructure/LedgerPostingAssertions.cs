using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.UnitTests.Infrastructure;

internal static class LedgerPostingAssertions
{
    public static long SignedMinorUnitSum(this LedgerPosting posting) =>
        posting.Legs.Sum(SignedMinorUnits);

    public static long DebitMinorUnits(this LedgerPosting posting, LedgerAccountType type) =>
        posting.Legs
            .Where(l => l.Account.Type == type && l.Direction == LedgerDirection.Debit)
            .Sum(l => l.Amount.ToMinorUnits());

    public static long CreditMinorUnits(this LedgerPosting posting, LedgerAccountType type) =>
        posting.Legs
            .Where(l => l.Account.Type == type && l.Direction == LedgerDirection.Credit)
            .Sum(l => l.Amount.ToMinorUnits());

    private static long SignedMinorUnits(PostingLeg leg) =>
        leg.Direction == LedgerDirection.Debit ? leg.Amount.ToMinorUnits() : -leg.Amount.ToMinorUnits();
}
