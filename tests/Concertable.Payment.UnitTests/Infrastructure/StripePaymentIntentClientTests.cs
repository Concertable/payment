using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripePaymentIntentClientTests
{
    private readonly Mock<IStripeApiClient> stripeClient = new();
    private readonly Mock<IStripeAccountClient> stripeAccountClient = new();
    private readonly StripePaymentIntentClient sut;

    public StripePaymentIntentClientTests()
    {
        stripeAccountClient
            .Setup(c => c.GetAccountStatusAsync("acct_test"))
            .ReturnsAsync(PayoutAccountStatus.Verified);
        sut = new StripePaymentIntentClient(
            stripeClient.Object,
            stripeAccountClient.Object,
            Mock.Of<IPaymentSessionConfigurator>(),
            NullLogger<StripePaymentIntentClient>.Instance);
    }

    [Fact]
    public async Task ChargeAsync_CardError_ReturnsTypedRejection()
    {
        stripeClient
            .Setup(c => c.CreatePaymentIntentAsync(It.IsAny<PaymentIntentCreateOptions>(), It.IsAny<RequestOptions?>()))
            .ThrowsAsync(new StripeException("declined")
            {
                StripeError = new StripeError { Type = "card_error" }
            });

        var result = await sut.ChargeAsync(Options());

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(new PaymentError.PaymentRejected(), error);
    }

    [Fact]
    public async Task ChargeAsync_DeclineCode_ReturnsTypedRejection()
    {
        stripeClient
            .Setup(c => c.CreatePaymentIntentAsync(It.IsAny<PaymentIntentCreateOptions>(), It.IsAny<RequestOptions?>()))
            .ThrowsAsync(new StripeException("declined")
            {
                StripeError = new StripeError { DeclineCode = "generic_decline" }
            });

        var result = await sut.ChargeAsync(Options());

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(new PaymentError.PaymentRejected(), error);
    }

    [Fact]
    public async Task ChargeAsync_StripeInfrastructureFailure_Propagates()
    {
        var exception = new StripeException("Stripe unavailable")
        {
            StripeError = new StripeError { Type = "api_error" }
        };
        stripeClient
            .Setup(c => c.CreatePaymentIntentAsync(It.IsAny<PaymentIntentCreateOptions>(), It.IsAny<RequestOptions?>()))
            .ThrowsAsync(exception);

        var thrown = await Assert.ThrowsAsync<StripeException>(() => sut.ChargeAsync(Options()));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task ChargeAsync_Cancellation_Propagates()
    {
        var exception = new OperationCanceledException();
        stripeClient
            .Setup(c => c.CreatePaymentIntentAsync(It.IsAny<PaymentIntentCreateOptions>(), It.IsAny<RequestOptions?>()))
            .ThrowsAsync(exception);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ChargeAsync(Options()));

        Assert.Same(exception, thrown);
    }

    private static StripeChargeOptions Options() =>
        new()
        {
            Amount = Money.Gbp(10),
            PaymentMethodId = "pm_test",
            StripeCustomerId = "cus_test",
            DestinationStripeId = "acct_test",
            ReceiptEmail = "payer@example.com",
            Metadata = new Dictionary<string, string>()
        };
}
