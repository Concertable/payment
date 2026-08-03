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
    private RequestOptions? reversalRequest;
    private RefundCreateOptions? refund;
    private RequestOptions? refundRequest;

    public StripeTransferClientTests()
    {
        this.stripeClient = new Mock<IStripeApiClient>();

        stripeClient
            .Setup(c => c.CreateTransferReversalAsync(
                "tr_test",
                It.IsAny<TransferReversalCreateOptions>(),
                It.IsAny<RequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TransferReversalCreateOptions, RequestOptions?, CancellationToken>((_, options, request, _) =>
            {
                reversal = options;
                reversalRequest = request;
            })
            .ReturnsAsync(new Stripe.TransferReversal());
        stripeClient
            .Setup(c => c.CreateRefundAsync(
                It.IsAny<RefundCreateOptions>(),
                It.IsAny<RequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<RefundCreateOptions, RequestOptions?, CancellationToken>((options, request, _) =>
            {
                refund = options;
                refundRequest = request;
            })
            .ReturnsAsync(new Stripe.Refund { Id = "re_test", Amount = 5500 });

        this.sut = new StripeTransferClient(
            stripeClient.Object,
            NullLogger<StripeTransferClient>.Instance);
    }

    [Fact]
    public async Task RefundAsync_UsesPayeeRefundForTransferReversalAndTotalForCustomerRefund()
    {
        var bindingId = Guid.NewGuid();
        var result = await sut.RefundAsync(new StripeRefundOptions
        {
            Amount = Money.Gbp(55),
            PaymentIntentId = "pi_test",
            TransferReversal = new("tr_test", Money.Gbp(50)),
            Metadata = new Dictionary<string, string>
            {
                [PaymentMetadataKeys.CommissionBindingId] = bindingId.ToString(),
                [PaymentMetadataKeys.CumulativeGrossRefundMinor] = "5500"
            }
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(reversal);
        Assert.Equal(5000, reversal.Amount);
        Assert.Equal(
            $"commission:{bindingId}:refund-reversal:5500",
            reversalRequest?.IdempotencyKey);
        Assert.NotNull(refund);
        Assert.Equal(5500, refund.Amount);
        Assert.Equal(
            $"commission:{bindingId}:refund:5500",
            refundRequest?.IdempotencyKey);
    }

    [Fact]
    public async Task RefundAsync_WithoutAuthorizationPreservesLegacyNonIdempotentRequest()
    {
        var result = await sut.RefundAsync(new StripeRefundOptions
        {
            Amount = Money.Gbp(55),
            PaymentIntentId = "pi_test",
            TransferReversal = new("tr_test", Money.Gbp(50)),
            Metadata = new Dictionary<string, string>
            {
                [PaymentMetadataKeys.CumulativeGrossRefundMinor] = "5500"
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Null(reversalRequest);
        Assert.Null(refundRequest);
    }
}
