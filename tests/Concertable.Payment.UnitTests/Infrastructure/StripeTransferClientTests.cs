using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeTransferClientTests
{
    private readonly Mock<IStripeApiClient> stripeClient;
    private readonly StripeTransferClient sut;

    private TransferReversalCreateOptions? reversal;
    private RefundCreateOptions? refund;

    public StripeTransferClientTests()
    {
        this.stripeClient = new Mock<IStripeApiClient>();

        stripeClient
            .Setup(c => c.CreateTransferReversalAsync("tr_test", It.IsAny<TransferReversalCreateOptions>()))
            .Callback<string, TransferReversalCreateOptions>((_, options) => reversal = options)
            .ReturnsAsync(new Stripe.TransferReversal());
        stripeClient
            .Setup(c => c.CreateRefundAsync(It.IsAny<RefundCreateOptions>()))
            .Callback<RefundCreateOptions>(options => refund = options)
            .ReturnsAsync(new Stripe.Refund { Id = "re_test", Amount = 5500 });

        this.sut = new StripeTransferClient(
            stripeClient.Object,
            NullLogger<StripeTransferClient>.Instance);
    }

    [Fact]
    public async Task RefundAsync_UsesPayeeRefundForTransferReversalAndTotalForCustomerRefund()
    {
        var result = await sut.RefundAsync(new StripeRefundOptions
        {
            Amount = Money.Gbp(55),
            PaymentIntentId = "pi_test",
            TransferReversal = new("tr_test", Money.Gbp(50)),
            Metadata = []
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(reversal);
        Assert.Equal(5000, reversal.Amount);
        Assert.NotNull(refund);
        Assert.Equal(5500, refund.Amount);
    }
}
