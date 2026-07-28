using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Settings;
using FluentResults;
using Microsoft.Extensions.Options;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class ManagerPaymentServiceTests
{
    private readonly Mock<IPaymentManager> paymentManager;
    private readonly Mock<IStripeAccountClient> stripeAccountClient;
    private readonly Mock<IStripeHoldClient> stripeHoldClient;
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository;
    private readonly Mock<ITransactionRepository> transactionRepository;

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    public ManagerPaymentServiceTests()
    {
        this.paymentManager = new Mock<IPaymentManager>();
        this.stripeAccountClient = new Mock<IStripeAccountClient>();
        this.stripeHoldClient = new Mock<IStripeHoldClient>();
        this.payoutAccountRepository = new Mock<IPayoutAccountRepository>();
        this.transactionRepository = new Mock<ITransactionRepository>();

        payoutAccountRepository
            .Setup(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayoutAccountWith("cus_test"));
    }

    private ManagerPaymentService SutWithFee(decimal fee) =>
        new(
            paymentManager.Object,
            stripeAccountClient.Object,
            stripeHoldClient.Object,
            payoutAccountRepository.Object,
            transactionRepository.Object,
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
        Assert.Equal(1200, captured.PlatformFee);
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
        Assert.Equal(0, captured.PlatformFee);
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

    private static PayoutAccountEntity PayoutAccountWith(string stripeCustomerId)
    {
        var account = PayoutAccountEntity.Create(Guid.NewGuid(), "payer@test.com");
        account.LinkCustomer(stripeCustomerId);
        return account;
    }
}
