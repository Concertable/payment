using Reunion;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Domain;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class EscrowServiceTests
{
    private readonly Mock<IPaymentManager> paymentManager;
    private readonly Mock<IEscrowRepository> escrowRepository;
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository;
    private readonly Mock<ILedgerService> ledger;
    private readonly Mock<ICommissionService> commissionService;
    private readonly FakeTimeProvider timeProvider;
    private readonly EscrowService sut;

    private readonly List<LedgerPosting> postings = [];

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    public EscrowServiceTests()
    {
        this.paymentManager = new Mock<IPaymentManager>();
        this.escrowRepository = new Mock<IEscrowRepository>();
        this.payoutAccountRepository = new Mock<IPayoutAccountRepository>();
        this.ledger = new Mock<ILedgerService>();
        this.commissionService = new Mock<ICommissionService>();

        ledger
            .Setup(l => l.StageAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerPosting, CancellationToken>((p, _) => postings.Add(p))
            .Returns(Task.CompletedTask);

        escrowRepository
            .Setup(r => r.TryReserveRefundGrossAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        this.timeProvider = new FakeTimeProvider();

        this.sut = SutWithFee(0m);

        payoutAccountRepository
            .Setup(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWith("cus_test"));
    }

    private EscrowService SutWithFee(decimal fee) =>
        new(
            paymentManager.Object,
            escrowRepository.Object,
            payoutAccountRepository.Object,
            ledger.Object,
            new FakeUnitOfWork(),
            commissionService.Object,
            new CommissionCalculator(),
            Options.Create(new PlatformFeeOptions { Fee = fee }),
            timeProvider,
            NullLogger<EscrowService>.Instance);

    [Fact]
    public async Task DepositBoundCommissionAsync_ExistingEscrowWithMismatchedGross_ReturnsCommissionFailure()
    {
        var bindingId = Guid.NewGuid();
        escrowRepository
            .Setup(r => r.GetByCommissionBindingIdAsync(bindingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingBoundEscrow(bindingId));
        commissionService
            .Setup(c => c.CalculateBoundAsync(
                bindingId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Money>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BoundCommission, CommissionError>.Failure(new CommissionError.GrossMismatch()));

        var result = await sut.DepositBoundCommissionAsync(
            payerId,
            payeeId,
            Money.Gbp(51),
            "pm_test",
            PaymentSession.OnSession,
            7,
            bindingId,
            "booking:7",
            null);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(new EscrowDepositError.CommissionFailure(new CommissionError.GrossMismatch()), error);
        paymentManager.Verify(
            p => p.HoldAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Money>(),
                It.IsAny<string>(),
                It.IsAny<PaymentSession>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CaptureBoundCommissionAsync_ExistingEscrowWithMismatchedGross_ReturnsCommissionFailure()
    {
        var bindingId = Guid.NewGuid();
        escrowRepository
            .Setup(r => r.GetByCommissionBindingIdAsync(bindingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingBoundEscrow(bindingId));
        commissionService
            .Setup(c => c.CalculateBoundAsync(
                bindingId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Money>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BoundCommission, CommissionError>.Failure(new CommissionError.GrossMismatch()));

        var result = await sut.CaptureBoundCommissionAsync(
            payerId,
            payeeId,
            Money.Gbp(51),
            "pi_existing",
            7,
            bindingId,
            "booking:7");

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(new EscrowCaptureError.CommissionFailure(new CommissionError.GrossMismatch()), error);
        paymentManager.Verify(
            p => p.CaptureAsync(It.IsAny<CaptureRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DepositAsync_OnSynchronousSuccess_PersistsEscrowAtHeld()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentOutcome, PaymentError>.Success(
                new PaymentOutcome { TransactionId = "pi_synced", RequiresAction = false }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<EscrowEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await sut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.TryGetValue(out var deposit));
        Assert.Equal(EscrowStatus.Held, deposit.Status);
        Assert.Null(deposit.ClientSecret);
        Assert.NotNull(captured);
        Assert.Equal(EscrowStatus.Held, captured.Status);
        Assert.Equal("pi_synced", captured.ChargeId);
        Assert.Equal(7, captured.BookingId);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.StripeClearing));
    }

    [Fact]
    public async Task DepositAsync_FinancialOperation_ForwardsOperationIdentity()
    {
        var operationId = Guid.NewGuid();
        paymentManager
            .Setup(p => p.HoldAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Money>(),
                It.IsAny<string>(),
                It.IsAny<PaymentSession>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                operationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOutcome { TransactionId = "pi_operation", RequiresAction = false });

        var result = await sut.DepositAsync(
            payerId,
            payeeId,
            Money.Gbp(50),
            "pm_test",
            PaymentSession.OnSession,
            7,
            operationId);

        Assert.True(result.IsSuccess);
        paymentManager.VerifyAll();
    }

    [Fact]
    public async Task DepositAsync_OnRequiresAction_PersistsEscrowAtPendingWithClientSecret()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentOutcome, PaymentError>.Success(new PaymentOutcome
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

        Assert.True(result.TryGetValue(out var deposit));
        Assert.Equal(EscrowStatus.Pending, deposit.Status);
        Assert.Equal("pi_3ds_secret_xyz", deposit.ClientSecret);
        Assert.NotNull(captured);
        Assert.Equal(EscrowStatus.Pending, captured.Status);
        Assert.Empty(postings);
    }

    [Fact]
    public async Task DepositAsync_OnStripeFailure_DoesNotPersistEscrow()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentOutcome, PaymentError>.Failure(new PaymentError.PaymentRejected()));

        var result = await sut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsFailure);
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

        Assert.True(result.TryGetValue(out var transfer));
        Assert.True(transfer.IsNone);
        paymentManager.Verify(
            p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_EscrowNotHeld_ReturnsNullResult()
    {
        var pendingEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEscrow);

        var result = await sut.ReleaseByBookingIdAsync(7);

        Assert.True(result.TryGetValue(out var transfer));
        Assert.True(transfer.IsNone);
        paymentManager.Verify(
            p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_EscrowHeld_ReleasesAndMutatesEntity()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        paymentManager
            .Setup(p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transfer, PaymentError>.Success(new Transfer("tr_test")));

        var result = await sut.ReleaseByBookingIdAsync(7);

        Assert.True(result.TryGetValue(out var transfer));
        Assert.True(transfer.TryGetValue(out var released));
        Assert.Equal("tr_test", released.TransferId);
        Assert.Equal(EscrowStatus.Released, heldEscrow.Status);
        Assert.Equal("tr_test", heldEscrow.TransferId);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_WithOperationId_BindsOperationAndPassesItToProvider()
    {
        var operationId = Guid.CreateVersion7();
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        ReleaseRequest? captured = null;
        paymentManager
            .Setup(p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReleaseRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(Result<Transfer, PaymentError>.Success(new Transfer("tr_operation")));

        var result = await sut.ReleaseByBookingIdAsync(operationId, 7);

        Assert.True(result.TryGetValue(out var transfer));
        Assert.True(transfer.TryGetValue(out var released));
        Assert.Equal("tr_operation", released.TransferId);
        Assert.Equal(operationId, captured?.OperationId);
        Assert.Equal(operationId, heldEscrow.ReleaseOperationId);
        Assert.Equal(SettlementOperationFingerprint.CurrentVersion, heldEscrow.ReleaseOperationFingerprintVersion);
        Assert.NotNull(heldEscrow.ReleaseOperationFingerprint);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_ReplayedOperation_ReturnsPersistedTransferWithoutProviderCall()
    {
        var operationId = Guid.CreateVersion7();
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();
        heldEscrow.BeginRelease(operationId, SettlementOperationFingerprint.CreateRelease(operationId, heldEscrow));
        heldEscrow.Release("tr_existing", DateTime.UtcNow);
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        var result = await sut.ReleaseByBookingIdAsync(operationId, 7);

        Assert.True(result.TryGetValue(out var transfer));
        Assert.True(transfer.TryGetValue(out var released));
        Assert.Equal("tr_existing", released.TransferId);
        paymentManager.Verify(
            manager => manager.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_ReusedOperationWithChangedIdentity_ReturnsConflict()
    {
        var firstOperationId = Guid.CreateVersion7();
        var secondOperationId = Guid.CreateVersion7();
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();
        heldEscrow.BeginRelease(
            firstOperationId,
            SettlementOperationFingerprint.CreateRelease(firstOperationId, heldEscrow));
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        var result = await sut.ReleaseByBookingIdAsync(secondOperationId, 7);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(new EscrowReleaseOperationError.OperationConflict(), error);
        paymentManager.Verify(
            manager => manager.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_NoEscrow_ReturnsNullResult()
    {
        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EscrowEntity?)null);

        var result = await sut.RefundByBookingIdAsync(99);

        Assert.True(result.TryGetValue(out var refund));
        Assert.True(refund.IsNone);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_AlreadyRefunded_IsNoOpSuccess()
    {
        var refundedEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        refundedEscrow.Confirm();
        refundedEscrow.RecordRefund(PaymentRefundEntity.CreateCompletedForEscrow(
            refundedEscrow.Id,
            "re_prior",
            5000,
            0,
            0,
            timeProvider.GetUtcNow()));

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedEscrow);

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.TryGetValue(out var refund));
        Assert.True(refund.IsNone);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_EscrowHeld_RefundsAndMutatesEntity()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Refund, PaymentError>.Success(new Refund("re_test")));

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.TryGetValue(out var refund));
        Assert.True(refund.TryGetValue(out var completed));
        Assert.Equal("re_test", completed.RefundId);
        Assert.Equal(EscrowStatus.Refunded, heldEscrow.Status);
        Assert.Equal("re_test", Assert.Single(heldEscrow.Refunds).StripeRefundId);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.StripeClearing));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public async Task RefundByBookingIdAsync_StripeRefundFails_ReleasesReservationAndFreesReservedGross()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Refund, PaymentError>.Failure(new PaymentError.PaymentRejected()));

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsFailure);
        Assert.Equal(EscrowStatus.Held, heldEscrow.Status);
        var reservation = Assert.Single(heldEscrow.Refunds);
        Assert.Equal(PaymentRefundStatus.Failed, reservation.Status);
        Assert.False(reservation.CountsTowardCumulative);
        escrowRepository.Verify(
            r => r.ReleaseReservedRefundGrossAsync(heldEscrow.Id, 5000, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Empty(postings);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_ProviderFailureAfterReservationTransition_ThrowsInvariantFailure()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => heldEscrow.ReleaseRefund(Assert.Single(heldEscrow.Refunds)))
            .ReturnsAsync(Result<Refund, PaymentError>.Failure(new PaymentError.PaymentRejected()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RefundByBookingIdAsync(7));

        Assert.Equal("Escrow refund reservation could not be released.", exception.Message);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_DestinationCharge_ReversesTransfer()
    {
        var releasedEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        releasedEscrow.Confirm();
        releasedEscrow.Release("tr_dest", timeProvider.GetUtcNow().DateTime);

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(releasedEscrow);
        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(releasedEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(releasedEscrow);

        RefundRequest? captured = null;
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result<Refund, PaymentError>.Success(new Refund("re_test")));

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal("tr_dest", captured.TransferReversal!.TransferId);
        Assert.Equal(EscrowStatus.Refunded, releasedEscrow.Status);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public async Task RefundAsync_AfterRelease_PartialRefundWithZeroFee_ReversesRefundedAmount()
    {
        var releasedEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");
        releasedEscrow.Confirm();
        releasedEscrow.Release("tr_dest", timeProvider.GetUtcNow().DateTime);

        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(releasedEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(releasedEscrow);

        RefundRequest? captured = null;
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result<Refund, PaymentError>.Success(new Refund("re_partial")));

        var result = await sut.RefundAsync(releasedEscrow.Id, Money.Gbp(10));

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(Money.Gbp(10), captured.Amount);
        Assert.Equal(Money.Gbp(10), captured.TransferReversal!.Amount);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(1000, posting.DebitMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(0, posting.DebitMinorUnits(LedgerAccountType.PlatformRevenue));
        Assert.Equal(1000, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public async Task RefundAsync_AfterRelease_PartialRefundWithFee_SplitsPayeeAndRevenueReversal()
    {
        var releasedEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(12), "pi_test");
        releasedEscrow.Confirm();
        releasedEscrow.Release("tr_dest", timeProvider.GetUtcNow().DateTime);

        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(releasedEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(releasedEscrow);

        RefundRequest? captured = null;
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result<Refund, PaymentError>.Success(new Refund("re_partial")));

        var result = await sut.RefundAsync(releasedEscrow.Id, Money.Gbp(55));

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(Money.Gbp(55), captured.Amount);
        Assert.Equal(Money.Gbp(50), captured.TransferReversal!.Amount);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(500, posting.DebitMinorUnits(LedgerAccountType.PlatformRevenue));
        Assert.Equal(5500, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public async Task RefundByBookingIdAsync_NotRefundableStatus_IsNoOpSuccess()
    {
        var pendingEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_test");

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingEscrow);

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.TryGetValue(out var refund));
        Assert.True(refund.IsNone);
        Assert.Equal(EscrowStatus.Pending, pendingEscrow.Status);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DepositAsync_WithPlatformFee_HoldsGrossPlusFeeAndSnapshotsFee()
    {
        var feeSut = SutWithFee(12m);

        Money heldAmount = default;
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Money, string, PaymentSession, IReadOnlyDictionary<string, string>, CancellationToken>((_, _, amount, _, _, _, _) => heldAmount = amount)
            .ReturnsAsync(Result<PaymentOutcome, PaymentError>.Success(
                new PaymentOutcome { TransactionId = "pi_fee", RequiresAction = false }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<EscrowEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await feeSut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsSuccess);
        Assert.Equal(Money.Gbp(62), heldAmount);
        Assert.NotNull(captured);
        Assert.Equal(6200, captured.PayerTotalMinor);
        Assert.Equal(1200, captured.CommissionGrossMinor);
    }

    [Fact]
    public async Task DepositAsync_ZeroFee_SnapshotsGrossWithNoFee()
    {
        paymentManager
            .Setup(p => p.HoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentOutcome, PaymentError>.Success(
                new PaymentOutcome { TransactionId = "pi_zero", RequiresAction = false }));

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<EscrowEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await sut.DepositAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(5000, captured.PayerTotalMinor);
        Assert.Equal(0, captured.CommissionGrossMinor);
    }

    [Fact]
    public async Task CaptureAsync_WithPlatformFee_SnapshotsGrossPlusFeeAndFee()
    {
        var feeSut = SutWithFee(12m);
        var operationId = Guid.NewGuid();
        CaptureRequest? captureRequest = null;

        paymentManager
            .Setup(p => p.CaptureAsync(It.IsAny<CaptureRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CaptureRequest, CancellationToken>((request, _) => captureRequest = request)
            .ReturnsAsync(UnitResult<PaymentError>.Success());

        EscrowEntity? captured = null;
        escrowRepository
            .Setup(r => r.AddAsync(It.IsAny<EscrowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<EscrowEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(() => captured!);

        var result = await feeSut.CaptureAsync(payerId, payeeId, Money.Gbp(50), "pi_held", 7, operationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(operationId, captureRequest!.OperationId);
        Assert.NotNull(captured);
        Assert.Equal(6200, captured.PayerTotalMinor);
        Assert.Equal(1200, captured.CommissionGrossMinor);
        Assert.Equal(EscrowStatus.Held, captured.Status);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.Receivable));
        Assert.Equal(6200, posting.CreditMinorUnits(LedgerAccountType.StripeClearing));
    }

    [Fact]
    public async Task CaptureAsync_ExistingBooking_ReturnsExistingEscrowWithoutCapturingAgain()
    {
        var existing = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_held");
        existing.Confirm();
        escrowRepository
            .Setup(repository => repository.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await sut.CaptureAsync(
            payerId,
            payeeId,
            Money.Gbp(50),
            "pi_held",
            7,
            Guid.NewGuid());

        Assert.True(result.TryGetValue(out var deposit));
        Assert.Equal("pi_held", deposit.ChargeId);
        paymentManager.Verify(
            manager => manager.CaptureAsync(It.IsAny<CaptureRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundByBookingIdAsync_PendingOperation_ResumesSameRefundReservation()
    {
        var operationId = Guid.NewGuid();
        var existing = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(0), "pi_held");
        existing.Confirm();
        var reservation = PaymentRefundEntity.CreatePendingForEscrow(
            existing.Id,
            5000,
            0,
            0,
            timeProvider.GetUtcNow(),
            operationId);
        existing.RecordRefund(reservation);
        escrowRepository
            .Setup(repository => repository.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        paymentManager
            .Setup(manager => manager.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Refund, PaymentError>.Success(new Refund("re_resumed")));

        var result = await sut.RefundByBookingIdAsync(7, null, "cancelled", operationId);

        Assert.True(result.TryGetValue(out var option));
        Assert.True(option.TryGetValue(out var refund));
        Assert.Equal("re_resumed", refund.RefundId);
        Assert.Equal(PaymentRefundStatus.Completed, reservation.Status);
        paymentManager.Verify(
            manager => manager.RefundAsync(
                It.Is<RefundRequest>(request =>
                    request.OperationId == operationId &&
                    request.Metadata[PaymentMetadataKeys.OperationId] == operationId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
        escrowRepository.Verify(
            repository => repository.TryReserveRefundGrossAsync(
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseByBookingIdAsync_WithPlatformFee_TransfersGrossOnly()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(12), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        ReleaseRequest? released = null;
        paymentManager
            .Setup(p => p.ReleaseAsync(It.IsAny<ReleaseRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReleaseRequest, CancellationToken>((r, _) => released = r)
            .ReturnsAsync(Result<Transfer, PaymentError>.Success(new Transfer("tr_test")));

        var result = await sut.ReleaseByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(released);
        Assert.Equal(Money.Gbp(50), released.Amount);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(6200, posting.DebitMinorUnits(LedgerAccountType.StripeClearing));
        Assert.Equal(5000, posting.CreditMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(1200, posting.CreditMinorUnits(LedgerAccountType.PlatformRevenue));
    }

    [Fact]
    public async Task RefundByBookingIdAsync_WithPlatformFee_RefundsFullChargedAmount()
    {
        var heldEscrow = EscrowEntity.Create(7, payerId, payeeId, Money.Gbp(50), Money.Gbp(12), "pi_test");
        heldEscrow.Confirm();

        escrowRepository
            .Setup(r => r.GetByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);
        escrowRepository
            .Setup(r => r.GetWithRefundsByIdAsync(heldEscrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldEscrow);

        RefundRequest? refunded = null;
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => refunded = r)
            .ReturnsAsync(Result<Refund, PaymentError>.Success(new Refund("re_test")));

        var result = await sut.RefundByBookingIdAsync(7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(refunded);
        Assert.Equal(Money.Gbp(62), refunded.Amount);
    }

    private EscrowEntity ExistingBoundEscrow(Guid bindingId)
    {
        var escrow = EscrowEntity.CreateBound(
            7,
            payerId,
            payeeId,
            bindingId,
            new Concertable.Payment.Domain.CommissionCalculation(
                Currency.Gbp,
                5000,
                1000,
                800,
                200,
                Percentage.From(20m),
                6000),
            "pi_existing");
        escrow.Confirm();
        return escrow;
    }

    private static PayoutAccountEntity PayoutAccountWith(string stripeCustomerId)
    {
        var account = PayoutAccountEntity.Create(Guid.NewGuid(), "payer@test.com");
        account.LinkCustomer(stripeCustomerId);
        return account;
    }
}
