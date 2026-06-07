using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class EscrowServiceTests
{
    private readonly Mock<IPaymentManager> paymentManager;
    private readonly Mock<IEscrowRepository> escrowRepository;
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository;
    private readonly FakeTimeProvider timeProvider;
    private readonly EscrowService sut;

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    public EscrowServiceTests()
    {
        this.paymentManager = new Mock<IPaymentManager>();
        this.escrowRepository = new Mock<IEscrowRepository>();
        this.payoutAccountRepository = new Mock<IPayoutAccountRepository>();
        this.timeProvider = new FakeTimeProvider();

        this.sut = new EscrowService(
            paymentManager.Object,
            escrowRepository.Object,
            payoutAccountRepository.Object,
            timeProvider,
            NullLogger<EscrowService>.Instance);

        payoutAccountRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWith("cus_test"));
    }

    [Fact]
    public async Task DepositAsync_OnSynchronousSuccess_PersistsEscrowAtHeld()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<HoldRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new PaymentResponse { TransactionId = "pi_synced", RequiresAction = false }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>()))
            .Callback<EscrowEntity>(e => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await sut.DepositAsync(payerId, payeeId, 50m, "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsSuccess);
        Assert.Equal(EscrowStatus.Held, result.Value.Status);
        Assert.Null(result.Value.ClientSecret);
        Assert.NotNull(captured);
        Assert.Equal(EscrowStatus.Held, captured.Status);
        Assert.Equal("pi_synced", captured.ChargeId);
        Assert.Equal(7, captured.BookingId);
    }

    [Fact]
    public async Task DepositAsync_OnRequiresAction_PersistsEscrowAtPendingWithClientSecret()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<HoldRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new PaymentResponse
            {
                TransactionId = "pi_3ds",
                RequiresAction = true,
                ClientSecret = "pi_3ds_secret_xyz"
            }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>()))
            .Callback<EscrowEntity>(e => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await sut.DepositAsync(payerId, payeeId, 50m, "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsSuccess);
        Assert.Equal(EscrowStatus.Pending, result.Value.Status);
        Assert.Equal("pi_3ds_secret_xyz", result.Value.ClientSecret);
        Assert.NotNull(captured);
        Assert.Equal(EscrowStatus.Pending, captured.Status);
    }

    [Fact]
    public async Task DepositAsync_OnStripeFailure_DoesNotPersistEscrow()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<HoldRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<PaymentResponse>("card_declined"));

        var result = await sut.DepositAsync(payerId, payeeId, 50m, "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsFailed);
        escrowRepository.Verify(
            r => r.AddAsync(It.IsAny<EscrowEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_NoEscrow_ReturnsNullResult()
    {
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EscrowEntity?)null);

        var result = await sut.ReleaseByBookingIdAsync(99);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        paymentManager.Verify(
            p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_EscrowNotHeld_ReturnsNullResult()
    {
        var pendingEscrow = EscrowEntity.Create(7, payerId, payeeId, 5000, "pi_test");
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEscrow);

        var result = await sut.ReleaseByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        paymentManager.Verify(
            p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_EscrowHeld_ReleasesAndMutatesEntity()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, 5000, "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id))
            .ReturnsAsync(heldEscrow);

        paymentManager
            .Setup(p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new TransferResponse("tr_test")));

        var result = await sut.ReleaseByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("tr_test", result.Value.TransferId);
        Assert.Equal(EscrowStatus.Released, heldEscrow.Status);
        Assert.Equal("tr_test", heldEscrow.TransferId);
    }

    private static PayoutAccountEntity PayoutAccountWith(string stripeCustomerId)
    {
        var account = PayoutAccountEntity.Create(Guid.NewGuid(), "payer@test.com");
        account.LinkCustomer(stripeCustomerId);
        return account;
    }
}
