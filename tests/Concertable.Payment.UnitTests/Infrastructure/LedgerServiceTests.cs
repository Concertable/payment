using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class LedgerServiceTests : IDisposable
{
    private readonly Mock<ILedgerAccountRepository> accountRepository;
    private readonly Mock<ILedgerTransactionRepository> transactionRepository;
    private readonly PaymentDbContext context;
    private readonly LedgerService sut;

    private readonly Guid payer = Guid.NewGuid();
    private readonly Guid payee = Guid.NewGuid();

    private LedgerTransactionEntity? posted;

    public LedgerServiceTests()
    {
        this.accountRepository = new Mock<ILedgerAccountRepository>();
        this.transactionRepository = new Mock<ILedgerTransactionRepository>();
        this.context = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ledger-service-unit-tests;Trusted_Connection=True")
                .Options,
            new PaymentConfigurationProvider());

        accountRepository
            .Setup(r => r.AddAsync(It.IsAny<LedgerAccountEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerAccountEntity a, CancellationToken _) => a);

        transactionRepository
            .Setup(r => r.AddAsync(It.IsAny<LedgerTransactionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerTransactionEntity, CancellationToken>((t, _) => posted = t)
            .ReturnsAsync((LedgerTransactionEntity t, CancellationToken _) => t);
        this.sut = new LedgerService(
            accountRepository.Object,
            transactionRepository.Object,
            context,
            new FakeTimeProvider());
    }

    private void AllAccountsMissing() =>
        accountRepository
            .Setup(r => r.FindAsync(It.IsAny<LedgerAccountType>(), It.IsAny<Guid?>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerAccountEntity?)null);

    private LedgerPosting Settlement(Money gross, Money fee) =>
        new(LedgerPostingType.DirectSettlement, "pi_test", BookingId: 7, PaymentIntentId: "pi_test",
        [
            new PostingLeg(new LedgerAccountRef(LedgerAccountType.Receivable, payer), LedgerDirection.Debit, gross + fee),
            new PostingLeg(new LedgerAccountRef(LedgerAccountType.Payable, payee), LedgerDirection.Credit, gross),
            new PostingLeg(new LedgerAccountRef(LedgerAccountType.PlatformRevenue, null), LedgerDirection.Credit, fee)
        ]);

    [Fact]
    public async Task PostAsync_WhenAccountsMissing_AddsAccountsAndTransaction()
    {
        AllAccountsMissing();

        await sut.PostAsync(Settlement(Money.Gbp(50), Money.Gbp(10)));

        accountRepository.Verify(r => r.AddAsync(It.IsAny<LedgerAccountEntity>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.NotNull(posted);
        Assert.Equal(3, posted!.Entries.Count);
    }

    [Fact]
    public async Task PostAsync_WhenAccountExists_ReusesItInsteadOfCreating()
    {
        AllAccountsMissing();
        accountRepository
            .Setup(r => r.FindAsync(LedgerAccountType.PlatformRevenue, null, It.IsAny<Currency>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LedgerAccountEntity.Create(LedgerAccountType.PlatformRevenue, null, Currency.Gbp));

        await sut.PostAsync(Settlement(Money.Gbp(50), Money.Gbp(10)));

        accountRepository.Verify(r => r.AddAsync(It.IsAny<LedgerAccountEntity>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PostAsync_WhenTwoLegsShareOneAccount_ResolvesItOnlyOnce()
    {
        AllAccountsMissing();

        var posting = new LedgerPosting(
            LedgerPostingType.DirectSettlement, "pi_test", BookingId: 7, PaymentIntentId: "pi_test",
        [
            new PostingLeg(new LedgerAccountRef(LedgerAccountType.Receivable, payer), LedgerDirection.Debit, Money.Gbp(100)),
            new PostingLeg(new LedgerAccountRef(LedgerAccountType.Payable, payee), LedgerDirection.Credit, Money.Gbp(40)),
            new PostingLeg(new LedgerAccountRef(LedgerAccountType.Payable, payee), LedgerDirection.Credit, Money.Gbp(60))
        ]);

        await sut.PostAsync(posting);

        accountRepository.Verify(r => r.AddAsync(It.IsAny<LedgerAccountEntity>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.NotNull(posted);
        var payableEntries = posted!.Entries.Where(e => e.Account.Type == LedgerAccountType.Payable).ToList();
        Assert.Equal(2, payableEntries.Count);
        Assert.Same(payableEntries[0].Account, payableEntries[1].Account);
    }

    [Fact]
    public async Task PostAsync_RepositoryFailure_Propagates()
    {
        AllAccountsMissing();
        transactionRepository
            .Setup(r => r.AddAsync(It.IsAny<LedgerTransactionEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PostAsync(Settlement(Money.Gbp(50), Money.Gbp(10))));
    }

    public void Dispose() => context.Dispose();
}
