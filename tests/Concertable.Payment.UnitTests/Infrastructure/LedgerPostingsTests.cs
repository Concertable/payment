using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class LedgerPostingsTests
{
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Payee = Guid.NewGuid();

    [Fact]
    public void DirectSettlement_WithFee_BalancesAndReconcilesChargeShareFee()
    {
        var posting = LedgerPostings.DirectSettlement(Payer, Payee, Money.Gbp(50), Money.Gbp(10), 7, "pi");

        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6000, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(1000, posting.CreditMinorUnits(LedgerAccountType.PlatformRevenue));
    }

    [Fact]
    public void DirectSettlement_FromSettlementEntity_ReconstructsChargeShareFee()
    {
        var settlement = SettlementTransactionEntity.Create(
            Payer, Payee, "pi_entity", amount: 6200, platformFee: 1200, TransactionStatus.Complete, bookingId: 7);

        var posting = LedgerPostings.DirectSettlement(settlement);

        Assert.Equal(7, posting.BookingId);
        Assert.Equal("pi_entity", posting.PaymentIntentId);
        Assert.Equal(LedgerPostingType.DirectSettlement, posting.PostingType);
        Assert.Equal("pi_entity", posting.ExternalId);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(1200, posting.CreditMinorUnits(LedgerAccountType.PlatformRevenue));
    }

    [Fact]
    public void DirectSettlement_ZeroFee_OmitsRevenueLegAndBalances()
    {
        var posting = LedgerPostings.DirectSettlement(Payer, Payee, Money.Gbp(50), Money.Gbp(0), 7, "pi");

        Assert.Equal(2, posting.Legs.Count);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.DoesNotContain(posting.Legs, l => l.Account.Type == LedgerAccountType.PlatformRevenue);
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Payable));
    }

    [Fact]
    public void EscrowHold_ChargesPayerIntoClearing()
    {
        var posting = LedgerPostings.EscrowHold(Payer, Money.Gbp(62), 7, "pi");

        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(6200, posting.CreditMinorUnits(LedgerAccountType.StripeClearing));
    }

    [Fact]
    public void EscrowRelease_WithFee_ClearsToPayeeAndRevenue()
    {
        var posting = LedgerPostings.EscrowRelease(Payee, Money.Gbp(50), Money.Gbp(12), 7, "pi", "tr_1");

        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.StripeClearing));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(1200, posting.CreditMinorUnits(LedgerAccountType.PlatformRevenue));
    }

    [Fact]
    public void EscrowRelease_ZeroFee_OmitsRevenueLeg()
    {
        var posting = LedgerPostings.EscrowRelease(Payee, Money.Gbp(50), Money.Gbp(0), 7, "pi", "tr_1");

        Assert.Equal(2, posting.Legs.Count);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.DoesNotContain(posting.Legs, l => l.Account.Type == LedgerAccountType.PlatformRevenue);
    }

    [Fact]
    public void EscrowRefundBeforeRelease_ReversesClearingToPayer()
    {
        var posting = LedgerPostings.EscrowRefundBeforeRelease(Payer, Money.Gbp(62), 7, "pi", "re_1");

        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.StripeClearing));
        Assert.Equal(6200, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public void EscrowRefundAfterRelease_WithFee_ReversesPayeeAndRevenueToPayer()
    {
        var posting = LedgerPostings.EscrowRefundAfterRelease(
            Payer, Payee, Money.Gbp(50), Money.Gbp(12), 7, "pi", "re_1");

        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(1200, posting.DebitMinorUnits(LedgerAccountType.PlatformRevenue));
        Assert.Equal(6200, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public void EscrowRefundAfterRelease_ZeroFee_OmitsRevenueLeg()
    {
        var posting = LedgerPostings.EscrowRefundAfterRelease(
            Payer, Payee, Money.Gbp(50), Money.Gbp(0), 7, "pi", "re_1");

        Assert.Equal(2, posting.Legs.Count);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }
}
