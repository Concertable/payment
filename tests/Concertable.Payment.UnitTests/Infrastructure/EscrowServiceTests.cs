using Concertable.Kernel.ValueObjects;
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
            .Setup(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWith("cus_test"));
    }

    [Fact]
    public async Task DepositAsync_OnSynchronousSuccess_PersistsEscrowAtHeld()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<HoldRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new PaymentOutcome { TransactionId = "pi_synced", RequiresAction = false }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<EscrowEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await sut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

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
            .ReturnsAsync(Result.Ok(new PaymentOutcome
            {
                TransactionId = "pi_3ds",
                RequiresAction = true,
                ClientSecret = "pi_3ds_secret_xyz"
            }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<EscrowEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await sut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

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
            .ReturnsAsync(Result.Fail<PaymentOutcome>("card_declined"));

        var result = await sut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsFailed);
        escrowRepository.Verify(
            r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()),
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
        var pendingEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), "pi_test");
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
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id))
            .ReturnsAsync(heldEscrow);

        paymentManager
            .Setup(p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new Transfer("tr_test")));

        var result = await sut.ReleaseByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("tr_test", result.Value.TransferId);
        Assert.Equal(EscrowStatus.Released, heldEscrow.Status);
        Assert.Equal("tr_test", heldEscrow.TransferId);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_NoEscrow_ReturnsNullResult()
    {
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EscrowEntity?)null);

        var result = await sut.RefundByBookingIdAsync(99);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_AlreadyRefunded_IsNoOpSuccess()
    {
        var refundedEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), "pi_test");
        refundedEscrow.Confirm();
        refundedEscrow.Refund("re_prior", timeProvider.GetUtcNow().DateTime);

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedEscrow);

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("re_prior", result.Value.RefundId);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_EscrowHeld_RefundsAndMutatesEntity()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id))
            .ReturnsAsync(heldEscrow);

        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new Refund("re_test")));

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("re_test", result.Value.RefundId);
        Assert.Equal(EscrowStatus.Refunded, heldEscrow.Status);
        Assert.Equal("re_test", heldEscrow.RefundId);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_DestinationCharge_ReversesTransfer()
    {
        var releasedEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), "pi_test");
        releasedEscrow.Confirm();
        releasedEscrow.Release("tr_dest", timeProvider.GetUtcNow().DateTime);

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(releasedEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(releasedEscrow.Id))
            .ReturnsAsync(releasedEscrow);

        RefundRequest? captured = null;
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result.Ok(new Refund("re_test")));

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal("tr_dest", captured.TransferId);
        Assert.Equal(EscrowStatus.Refunded, releasedEscrow.Status);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_NotRefundableStatus_IsNoOpSuccess()
    {
        var pendingEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), "pi_test");

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEscrow);

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(EscrowStatus.Pending, pendingEscrow.Status);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PayoutAccountEntity PayoutAccountWith(string stripeCustomerId)
    {
        var account = PayoutAccountEntity.Create(Guid.NewGuid(), "payer@test.com");
        account.LinkCustomer(stripeCustomerId);
        return account;
    }
}
