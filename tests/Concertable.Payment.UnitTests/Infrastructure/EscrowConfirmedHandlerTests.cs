using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class EscrowConfirmedHandlerTests
{
    private readonly Mock<IEscrowRepository> escrowRepository;
    private readonly Mock<ILedger> ledger;
    private readonly EscrowConfirmedHandler sut;

    private readonly List<LedgerPosting> postings = [];

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    public EscrowConfirmedHandlerTests()
    {
        this.escrowRepository = new Mock<IEscrowRepository>();
        this.ledger = new Mock<ILedger>();

        ledger
            .Setup(l => l.PostAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerPosting, CancellationToken>((p, _) => postings.Add(p))
            .ReturnsAsync((LedgerPosting _, CancellationToken _) => null!);

        this.sut = new EscrowConfirmedHandler(escrowRepository.Object, ledger.Object, NullLogger<EscrowConfirmedHandler>.Instance);
    }

    private static PaymentSucceededEvent EventFor(string chargeId) =>
        new(chargeId, new Dictionary<string, string>());

    [Fact]
    public async Task HandleAsync_PendingEscrow_ConfirmsSavesAndPostsHold()
    {
        var escrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(12), "pi_3ds");
        escrowRepository
            .Setup(r => r.GetByChargeIdAsync("pi_3ds", It.IsAny<CancellationToken>()))
            .ReturnsAsync(escrow);

        await sut.HandleAsync(EventFor("pi_3ds"), CancellationToken.None);

        Assert.Equal(EscrowStatus.Held, escrow.Status);
        escrowRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        var posting = Assert.Single(postings);
        Assert.Equal(7, posting.BookingId);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(6200, posting.CreditMinorUnits(LedgerAccountType.StripeClearing));
    }

    [Fact]
    public async Task HandleAsync_AlreadyHeldEscrow_DoesNotSaveOrPost()
    {
        var escrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(12), "pi_dup");
        escrow.Confirm();
        escrowRepository
            .Setup(r => r.GetByChargeIdAsync("pi_dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(escrow);

        await sut.HandleAsync(EventFor("pi_dup"), CancellationToken.None);

        escrowRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(postings);
    }

    [Fact]
    public async Task HandleAsync_NoEscrow_IsNoOp()
    {
        escrowRepository
            .Setup(r => r.GetByChargeIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EscrowEntity?)null);

        await sut.HandleAsync(EventFor("pi_missing"), CancellationToken.None);

        Assert.Empty(postings);
    }
}
