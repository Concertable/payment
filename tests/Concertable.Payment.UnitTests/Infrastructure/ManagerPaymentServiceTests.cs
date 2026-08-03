using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using FluentResults;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class ManagerPaymentServiceTests
{
    private readonly Mock<IPaymentManager> paymentManager;
    private readonly Mock<IStripeAccountClient> stripeAccountClient;
    private readonly Mock<IStripeHoldClient> stripeHoldClient;
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository;
    private readonly Mock<ITransactionRepository> transactionRepository;
    private readonly Mock<ICommissionService> commissionService;
    private readonly Mock<ILedgerService> ledger;

    private readonly List<LedgerPosting> postings = [];

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    public ManagerPaymentServiceTests()
    {
        this.paymentManager = new Mock<IPaymentManager>();
        this.stripeAccountClient = new Mock<IStripeAccountClient>();
        this.stripeHoldClient = new Mock<IStripeHoldClient>();
        this.payoutAccountRepository = new Mock<IPayoutAccountRepository>();
        this.transactionRepository = new Mock<ITransactionRepository>();
        this.commissionService = new Mock<ICommissionService>();
        this.ledger = new Mock<ILedgerService>();

        ledger
            .Setup(l => l.StageAsync(It.IsAny<LedgerPosting>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerPosting, CancellationToken>((p, _) => postings.Add(p))
            .Returns(Task.CompletedTask);

        payoutAccountRepository
            .Setup(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWith("cus_test"));

        transactionRepository
            .Setup(r => r.TryReserveSettlementRefundGrossAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private ManagerPaymentService SutWithFee(decimal fee) =>
        new(
            paymentManager.Object,
            stripeAccountClient.Object,
            stripeHoldClient.Object,
            payoutAccountRepository.Object,
            transactionRepository.Object,
            commissionService.Object,
            new CommissionCalculator(),
            ledger.Object,
            new FakeUnitOfWork(),
            new FakeTimeProvider(),
            Options.Create(new PlatformFeeOptions { Fee = fee }));

    [Fact]
    public async Task PayAsync_WithPlatformFee_ChargesGrossPlusFeeAndSnapshotsFee()
    {
        var sut = SutWithFee(12m);

        Money chargeAmount = default, payeeAmount = default;
        paymentManager
            .Setup(p => p.SettleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Money, Money, string, PaymentSession, IReadOnlyDictionary<string, string>, CancellationToken>((_, _, charge, payee, _, _, _, _) => { chargeAmount = charge; payeeAmount = payee; })
            .ReturnsAsync(Result.Ok(new PaymentOutcome { TransactionId = "pi_fee", RequiresAction = false }));

        SettlementTransactionEntity? captured = null;
        transactionRepository
            .Setup(r => r.CreateAsync(It.IsAny<TransactionEntity>()))
            .Callback<TransactionEntity>(e => captured = (SettlementTransactionEntity)e)
            .Returns(Task.CompletedTask);

        var result = await sut.PayAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsSuccess);
        Assert.Equal(Money.Gbp(62), chargeAmount);
        Assert.Equal(Money.Gbp(50), payeeAmount);
        Assert.NotNull(captured);
        Assert.Equal(6200, captured.Amount);
        Assert.Equal(1200, captured.CommissionGrossMinor);

        var posting = Assert.Single(postings);
        Assert.Equal(7, posting.BookingId);
        Assert.Equal("pi_fee", posting.PaymentIntentId);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(1200, posting.CreditMinorUnits(LedgerAccountType.PlatformRevenue));
    }

    [Fact]
    public async Task PayAsync_ZeroFee_ChargesGrossWithNoFee()
    {
        var sut = SutWithFee(0m);

        paymentManager
            .Setup(p => p.SettleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<PaymentSession>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new PaymentOutcome { TransactionId = "pi_zero", RequiresAction = false }));

        SettlementTransactionEntity? captured = null;
        transactionRepository
            .Setup(r => r.CreateAsync(It.IsAny<TransactionEntity>()))
            .Callback<TransactionEntity>(e => captured = (SettlementTransactionEntity)e)
            .Returns(Task.CompletedTask);

        var result = await sut.PayAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, bookingId: 7);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(5000, captured.Amount);
        Assert.Equal(0, captured.CommissionGrossMinor);

        var posting = Assert.Single(postings);
        Assert.Equal(2, posting.Legs.Count);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.DoesNotContain(posting.Legs, l => l.Account.Type == LedgerAccountType.PlatformRevenue);
    }

    [Fact]
    public async Task CreateHoldSessionAsync_WithPlatformFee_RingFencesGrossPlusFee()
    {
        var sut = SutWithFee(12m);

        Money held = default;
        stripeAccountClient
            .Setup(c => c.CreateHoldSessionAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Money, IReadOnlyDictionary<string, string>, CancellationToken>((_, amount, _, _) => held = amount)
            .ReturnsAsync(new CheckoutSession("cs_secret", "sess_secret", "cus_test"));

        await sut.CreateHoldSessionAsync(payerId, Money.Gbp(50), new Dictionary<string, string>());

        Assert.Equal(Money.Gbp(62), held);
    }

    [Fact]
    public async Task CreateBoundCommissionHoldSessionAsync_FirstCall_CreatesSessionAndBindsIntent()
    {
        var sut = SutWithFee(0m);
        var bindingId = Guid.NewGuid();
        var authorized = BoundCommissionFor(bindingId);

        commissionService
            .Setup(c => c.FindBoundPaymentIntentAsync(bindingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        commissionService
            .Setup(c => c.CalculateBoundAsync(
                bindingId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Currency>(),
                It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(authorized));
        stripeAccountClient
            .Setup(c => c.CreateHoldSessionAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSession("cs_new_secret", "sess_secret", "cus_test", "pi_hold_new"));

        var result = await sut.CreateBoundCommissionHoldSessionAsync(
            payerId, grossMinor: 5000, Currency.Gbp, new Dictionary<string, string>(),
            bindingId, "booking:7", stripeSetupIntentId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("cs_new_secret", result.Value.ClientSecret);
        commissionService.Verify(
            c => c.BindPaymentIntent(authorized.Binding, "pi_hold_new"), Times.Once);
        stripeAccountClient.Verify(
            c => c.GetHoldSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBoundCommissionHoldSessionAsync_Retry_ReturnsExistingSessionWithoutRebinding()
    {
        var sut = SutWithFee(0m);
        var bindingId = Guid.NewGuid();
        var authorized = BoundCommissionFor(bindingId, boundIntentId: "pi_hold_bound");

        commissionService
            .Setup(c => c.FindBoundPaymentIntentAsync(bindingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pi_hold_bound");
        string? suppliedPaymentIntent = "sentinel";
        commissionService
            .Setup(c => c.CalculateBoundAsync(
                bindingId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Currency>(),
                It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, Currency, long, string?, string?, CancellationToken>(
                (_, _, _, _, _, pi, _, _) => suppliedPaymentIntent = pi)
            .ReturnsAsync(Result.Ok(authorized));
        stripeAccountClient
            .Setup(c => c.GetHoldSessionAsync("cus_test", "pi_hold_bound", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSession("cs_existing_secret", "sess_secret", "cus_test", "pi_hold_bound"));

        var result = await sut.CreateBoundCommissionHoldSessionAsync(
            payerId, grossMinor: 5000, Currency.Gbp, new Dictionary<string, string>(),
            bindingId, "booking:7", stripeSetupIntentId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("cs_existing_secret", result.Value.ClientSecret);
        Assert.Equal("pi_hold_bound", suppliedPaymentIntent);
        stripeAccountClient.Verify(
            c => c.CreateHoldSessionAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        commissionService.Verify(
            c => c.BindPaymentIntent(It.IsAny<CommissionBindingEntity>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBoundCommissionHoldSessionAsync_DifferentSuppliedIntent_FailsClosedWithoutSession()
    {
        var sut = SutWithFee(0m);
        var bindingId = Guid.NewGuid();

        commissionService
            .Setup(c => c.FindBoundPaymentIntentAsync(bindingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pi_hold_bound");
        commissionService
            .Setup(c => c.CalculateBoundAsync(
                bindingId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Currency>(),
                It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("commission_binding_intent_mismatch"));

        var result = await sut.CreateBoundCommissionHoldSessionAsync(
            payerId, grossMinor: 5000, Currency.Gbp, new Dictionary<string, string>(),
            bindingId, "booking:7", stripeSetupIntentId: "seti_different");

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "commission_binding_intent_mismatch");
        stripeAccountClient.Verify(
            c => c.GetHoldSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        stripeAccountClient.Verify(
            c => c.CreateHoldSessionAsync(It.IsAny<string>(), It.IsAny<Money>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundBoundCommissionByBookingIdAsync_FullRefund_RecordsDurableRowAndReversesTransfer()
    {
        var sut = SutWithFee(0m);
        var settlement = CompletedAuthorizedSettlement();

        transactionRepository
            .Setup(r => r.GetSettlementWithRefundsByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        RefundRequest? captured = null;
        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(Result.Ok(new Refund("re_settlement")));

        var result = await sut.RefundBoundCommissionByBookingIdAsync(7, 5000, Currency.Gbp);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.True(captured.ReverseTransfer);
        Assert.Equal(Money.Gbp(60), captured.Amount);

        var recorded = Assert.Single(settlement.Refunds);
        Assert.Equal("re_settlement", recorded.StripeRefundId);
        Assert.Equal(5000, recorded.GrossRefundedMinor);
        Assert.Equal(1000, recorded.CommissionRefundedMinor);
        Assert.Equal(200, recorded.CommissionVatReversedMinor);
        Assert.Equal(settlement.Id, recorded.SettlementTransactionId);
        Assert.Null(recorded.EscrowId);

        var posting = Assert.Single(postings);
        Assert.Equal(0, posting.SignedMinorUnitSum());
        Assert.Equal(5000, posting.DebitMinorUnits(LedgerAccountType.Payable));
        Assert.Equal(800, posting.DebitMinorUnits(LedgerAccountType.PlatformRevenue));
        Assert.Equal(200, posting.DebitMinorUnits(LedgerAccountType.VatLiability));
        Assert.Equal(6000, posting.CreditMinorUnits(LedgerAccountType.Receivable));
    }

    [Fact]
    public async Task RefundBoundCommissionByBookingIdAsync_StripeRefundFails_ReleasesReservationAndFreesReservedGross()
    {
        var sut = SutWithFee(0m);
        var settlement = CompletedAuthorizedSettlement();

        transactionRepository
            .Setup(r => r.GetSettlementWithRefundsByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        paymentManager
            .Setup(p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<Refund>("stripe_declined"));

        var result = await sut.RefundBoundCommissionByBookingIdAsync(7, 5000, Currency.Gbp);

        Assert.True(result.IsFailed);
        var reservation = Assert.Single(settlement.Refunds);
        Assert.Equal(PaymentRefundStatus.Failed, reservation.Status);
        Assert.False(reservation.CountsTowardCumulative);
        transactionRepository.Verify(
            r => r.ReleaseReservedSettlementRefundGrossAsync(settlement.Id, 5000, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Empty(postings);
    }

    [Fact]
    public async Task RefundBoundCommissionByBookingIdAsync_ExceedsRemainingGross_Fails()
    {
        var sut = SutWithFee(0m);
        var settlement = CompletedAuthorizedSettlement();

        transactionRepository
            .Setup(r => r.GetSettlementWithRefundsByBookingIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settlement);

        var result = await sut.RefundBoundCommissionByBookingIdAsync(7, 5001, Currency.Gbp);

        Assert.True(result.IsFailed);
        Assert.Empty(settlement.Refunds);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefundBoundCommissionByBookingIdAsync_NoSettlement_IsNoOpSuccess()
    {
        var sut = SutWithFee(0m);

        transactionRepository
            .Setup(r => r.GetSettlementWithRefundsByBookingIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SettlementTransactionEntity?)null);

        var result = await sut.RefundBoundCommissionByBookingIdAsync(7, 5000, Currency.Gbp);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        paymentManager.Verify(
            p => p.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SettlementTransactionEntity CompletedAuthorizedSettlement()
    {
        var settlement = SettlementTransactionEntity.CreateBound(
            payerId,
            payeeId,
            "pi_settlement",
            new Concertable.Payment.Domain.CommissionCalculation(
                Currency.Gbp,
                5000,
                1000,
                800,
                200,
                Percentage.From(20m),
                6000),
            TransactionStatus.Pending,
            bookingId: 7,
            commissionBindingId: Guid.NewGuid());
        settlement.Complete();
        return settlement;
    }

    private BoundCommission BoundCommissionFor(Guid bindingId, string? boundIntentId = null)
    {
        var configuration = CommissionConfigurationEntity.Create(
            Guid.NewGuid(),
            Percentage.From(20m),
            DateTimeOffset.UtcNow);
        var terms = configuration.Terms;
        var binding = CommissionBindingEntity.Create(
            configuration, Currency.Gbp, "booking:7", payerId.ToString(), DateTimeOffset.UtcNow);
        if (boundIntentId is not null)
            binding.BindPaymentIntent(boundIntentId);
        var calculation = new Concertable.Payment.Domain.CommissionCalculation(
            Currency.Gbp,
            5000,
            1000,
            800,
            200,
            Percentage.From(20m),
            6000);
        return new BoundCommission(binding, terms, calculation);
    }

    private static PayoutAccountEntity PayoutAccountWith(string stripeCustomerId)
    {
        var account = PayoutAccountEntity.Create(Guid.NewGuid(), "payer@test.com");
        account.LinkCustomer(stripeCustomerId);
        return account;
    }
}
