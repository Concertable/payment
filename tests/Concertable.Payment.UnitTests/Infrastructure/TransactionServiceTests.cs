using Concertable.Kernel.Identity;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Services;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class TransactionServiceTests
{
    private readonly Mock<ICurrentUser> currentUser;
    private readonly Mock<ITransactionRepository> repository;
    private readonly Mock<ITransactionMapper> mapper;
    private readonly Mock<ILedger> ledger;
    private readonly TransactionService sut;

    private readonly List<LedgerPosting> postings = [];

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    public TransactionServiceTests()
    {
        this.currentUser = new Mock<ICurrentUser>();
        this.repository = new Mock<ITransactionRepository>();
        this.mapper = new Mock<ITransactionMapper>();
        this.ledger = new Mock<ILedger>();

        ledger
            .Setup(l => l.PostAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerPosting, CancellationToken>((p, _) => postings.Add(p))
            .ReturnsAsync((LedgerPosting _, CancellationToken _) => null!);

        this.sut = new TransactionService(currentUser.Object, repository.Object, mapper.Object, ledger.Object);
    }

    [Fact]
    public async Task CompleteAsync_SettlementPending_CompletesAndPostsDirectSettlement()
    {
        var settlement = SettlementTransactionEntity.Create(
            payerId, payeeId, "pi_3ds", amount: 6200, platformFee: 1200, TransactionStatus.Pending, bookingId: 7);
        repository
            .Setup(r => r.GetByPaymentIntentIdAsync("pi_3ds"))
            .ReturnsAsync(settlement);

        await sut.CompleteAsync("pi_3ds");

        Assert.Equal(TransactionStatus.Complete, settlement.Status);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);

        var posting = Assert.Single(postings);
        Assert.Equal(7, posting.BookingId);
        Assert.Equal("pi_3ds", posting.PaymentIntentId);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(1200, posting.CreditMinorUnits(LedgerAccountType.PlatformRevenue));
    }

    [Fact]
    public async Task CompleteAsync_SettlementAlreadyComplete_DoesNotSaveOrPost()
    {
        var settlement = SettlementTransactionEntity.Create(
            payerId, payeeId, "pi_done", amount: 6200, platformFee: 1200, TransactionStatus.Complete, bookingId: 7);
        repository
            .Setup(r => r.GetByPaymentIntentIdAsync("pi_done"))
            .ReturnsAsync(settlement);

        await sut.CompleteAsync("pi_done");

        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Empty(postings);
    }

    [Fact]
    public async Task CompleteAsync_LedgerStagingFails_RetryCommitsStateAndPostingTogether()
    {
        var failedAttempt = SettlementTransactionEntity.Create(
            payerId, payeeId, "pi_retry", amount: 6200, platformFee: 1200, TransactionStatus.Pending, bookingId: 7);
        var retry = SettlementTransactionEntity.Create(
            payerId, payeeId, "pi_retry", amount: 6200, platformFee: 1200, TransactionStatus.Pending, bookingId: 7);
        repository
            .SetupSequence(r => r.GetByPaymentIntentIdAsync("pi_retry"))
            .ReturnsAsync(failedAttempt)
            .ReturnsAsync(retry);
        ledger
            .SetupSequence(l => l.PostAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Ledger staging failed"))
            .ReturnsAsync((LedgerTransactionEntity)null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteAsync("pi_retry"));

        repository.Verify(r => r.SaveChangesAsync(), Times.Never);

        await sut.CompleteAsync("pi_retry");

        Assert.Equal(TransactionStatus.Complete, retry.Status);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        ledger.Verify(l => l.PostAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CompleteAsync_NonSettlement_CompletesButDoesNotPost()
    {
        var ticket = TicketTransactionEntity.Create(
            payerId, payeeId, "pi_ticket", amount: 2000, TransactionStatus.Pending, concertId: 3);
        repository
            .Setup(r => r.GetByPaymentIntentIdAsync("pi_ticket"))
            .ReturnsAsync(ticket);

        await sut.CompleteAsync("pi_ticket");

        Assert.Equal(TransactionStatus.Complete, ticket.Status);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
        Assert.Empty(postings);
    }

    [Fact]
    public async Task CompleteAsync_NoTransaction_IsNoOp()
    {
        repository
            .Setup(r => r.GetByPaymentIntentIdAsync(It.IsAny<string>()))
            .ReturnsAsync((TransactionEntity?)null);

        await sut.CompleteAsync("pi_missing");

        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Empty(postings);
    }
}
