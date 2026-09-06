using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class LedgerTransactionEntityTests
{
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Payee = Guid.NewGuid();
    private static readonly PaymentOperationReference Reference = new("settlement", "order:7");

    private static LedgerAccountEntity Account(LedgerAccountType type, Guid? ownerId = null) =>
        LedgerAccountEntity.Create(type, ownerId, Currency.Gbp);

    private static LedgerTransactionEntity PostSettlement(Money gross, Money fee) =>
        LedgerTransactionEntity.Post(
            postingType: LedgerPostingType.DirectSettlement,
            externalId: "pi_test",
            reference: Reference,
            paymentIntentId: "pi_test",
            occurredAt: DateTime.UtcNow,
            legs:
            [
                new LedgerLeg(Account(LedgerAccountType.Receivable, Payer), LedgerDirection.Debit, gross + fee),
                new LedgerLeg(Account(LedgerAccountType.Payable, Payee), LedgerDirection.Credit, gross),
                new LedgerLeg(Account(LedgerAccountType.PlatformRevenue), LedgerDirection.Credit, fee)
            ]);

    [Fact]
    public void Post_BalancedSettlement_ProducesSignedEntriesSummingToZero()
    {
        var transaction = PostSettlement(Money.Gbp(50), Money.Gbp(10));

        Assert.Equal(3, transaction.Entries.Count);
        Assert.Equal(0, transaction.Entries.Sum(e => e.Amount));

        var receivable = transaction.Entries.Single(e => e.Direction == LedgerDirection.Debit);
        Assert.Equal(6000, receivable.Amount);

        var revenue = transaction.Entries.Single(e => e.Account.Type == LedgerAccountType.PlatformRevenue);
        Assert.Equal(-1000, revenue.Amount);
        Assert.Equal(LedgerDirection.Credit, revenue.Direction);
    }

    [Fact]
    public void Post_CarriesCorrelationKeys()
    {
        var transaction = PostSettlement(Money.Gbp(50), Money.Gbp(10));

        Assert.Equal(Reference.OperationType, transaction.OperationType);
        Assert.Equal(Reference.ClientReference, transaction.ClientReference);
        Assert.Equal("pi_test", transaction.PaymentIntentId);
        Assert.Equal(LedgerPostingType.DirectSettlement, transaction.PostingType);
        Assert.Equal("pi_test", transaction.ExternalId);
    }

    [Fact]
    public void Post_UnbalancedLegs_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => LedgerTransactionEntity.Post(
            postingType: LedgerPostingType.DirectSettlement,
            externalId: "pi_test",
            reference: Reference,
            paymentIntentId: null,
            occurredAt: DateTime.UtcNow,
            legs:
            [
                new LedgerLeg(Account(LedgerAccountType.Receivable, Payer), LedgerDirection.Debit, Money.Gbp(60)),
                new LedgerLeg(Account(LedgerAccountType.Payable, Payee), LedgerDirection.Credit, Money.Gbp(50))
            ]));

        Assert.Contains("balance", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Post_FewerThanTwoLegs_Throws() =>
        Assert.Throws<DomainException>(() => LedgerTransactionEntity.Post(
            postingType: LedgerPostingType.DirectSettlement,
            externalId: "pi_test",
            reference: Reference,
            paymentIntentId: null,
            occurredAt: DateTime.UtcNow,
            legs: [new LedgerLeg(Account(LedgerAccountType.PlatformRevenue), LedgerDirection.Credit, Money.Gbp(10))]));

    [Fact]
    public void Post_NonPositiveAmount_Throws() =>
        Assert.Throws<DomainException>(() => LedgerTransactionEntity.Post(
            postingType: LedgerPostingType.DirectSettlement,
            externalId: "pi_test",
            reference: Reference,
            paymentIntentId: null,
            occurredAt: DateTime.UtcNow,
            legs:
            [
                new LedgerLeg(Account(LedgerAccountType.Receivable, Payer), LedgerDirection.Debit, Money.Gbp(0)),
                new LedgerLeg(Account(LedgerAccountType.PlatformRevenue), LedgerDirection.Credit, Money.Gbp(0))
            ]));

    [Fact]
    public void Entries_IsReadOnly_CannotBeMutatedByCastingToList()
    {
        var transaction = PostSettlement(Money.Gbp(50), Money.Gbp(10));

        Assert.IsNotType<List<LedgerEntryEntity>>(transaction.Entries);
    }
}
