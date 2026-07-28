using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure.Services;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentManagerTests
{
    private readonly Mock<IPayoutAccountRepository> payoutAccountRepository;
    private readonly Mock<IStripePaymentIntentClientFactory> intentClientFactory;
    private readonly Mock<IStripePaymentIntentClient> intentClient;
    private readonly Mock<IStripeTransferClient> transferClient;
    private readonly Mock<IStripeHoldClient> stripeHoldClient;

    private readonly Guid payerId = Guid.NewGuid();
    private readonly Guid payeeId = Guid.NewGuid();

    private readonly IReadOnlyDictionary<string, string> metadata =
        new Dictionary<string, string> { [PaymentMetadataKeys.Type] = TransactionTypes.Settlement };

    public PaymentManagerTests()
    {
        this.payoutAccountRepository = new Mock<IPayoutAccountRepository>();
        this.intentClientFactory = new Mock<IStripePaymentIntentClientFactory>();
        this.intentClient = new Mock<IStripePaymentIntentClient>();
        this.transferClient = new Mock<IStripeTransferClient>();
        this.stripeHoldClient = new Mock<IStripeHoldClient>();

        payoutAccountRepository
            .Setup(r => r.GetByOwnerIdAsync(payerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayerAccount());
        payoutAccountRepository
            .Setup(r => r.GetByOwnerIdAsync(payeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayeeAccount());

        intentClientFactory
            .Setup(f => f.Create(It.IsAny<PaymentSession>()))
            .Returns(intentClient.Object);
    }

    private PaymentManager Sut() =>
        new(
            payoutAccountRepository.Object,
            intentClientFactory.Object,
            transferClient.Object,
            stripeHoldClient.Object,
            NullLogger<PaymentManager>.Instance);

    [Fact]
    public async Task SettleAsync_ChargesFullAmountButTransfersOnlyThePayeeShare()
    {
        StripeChargeOptions? opts = null;
        intentClient
            .Setup(c => c.ChargeAsync(It.IsAny<StripeChargeOptions>()))
            .Callback<StripeChargeOptions>(o => opts = o)
            .ReturnsAsync(Result.Ok(new PaymentOutcome { TransactionId = "pi_test", RequiresAction = false }));

        await Sut().SettleAsync(payerId, payeeId, Money.Gbp(62), Money.Gbp(50), "pm_test", PaymentSession.OnSession, metadata);

        Assert.NotNull(opts);
        Assert.Equal(Money.Gbp(62), opts.Amount);
        Assert.Equal(Money.Gbp(50), opts.TransferAmount);
    }

    [Fact]
    public async Task ChargeAsync_LeavesTransferAmountNull_SoTheWholeChargeIsForwarded()
    {
        StripeChargeOptions? opts = null;
        intentClient
            .Setup(c => c.ChargeAsync(It.IsAny<StripeChargeOptions>()))
            .Callback<StripeChargeOptions>(o => opts = o)
            .ReturnsAsync(Result.Ok(new PaymentOutcome { TransactionId = "pi_test", RequiresAction = false }));

        await Sut().ChargeAsync(payerId, payeeId, Money.Gbp(50), "pm_test", PaymentSession.OnSession, metadata);

        Assert.NotNull(opts);
        Assert.Equal(Money.Gbp(50), opts.Amount);
        Assert.Null(opts.TransferAmount);
    }

    private PayoutAccountEntity PayerAccount()
    {
        var account = PayoutAccountEntity.Create(payerId, "payer@test.com");
        account.LinkCustomer("cus_test");
        return account;
    }

    private PayoutAccountEntity PayeeAccount()
    {
        var account = PayoutAccountEntity.Create(payeeId, "payee@test.com");
        account.LinkAccount("acct_test");
        return account;
    }
}
