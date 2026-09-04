using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Application.Errors;
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

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(new PaymentError.PaymentRejected(), rejection.Error);
        Assert.Equal(PaymentRecovery.NewPaymentMethod, rejection.Recovery);
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

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(new PaymentError.PaymentRejected(), rejection.Error);
        Assert.Equal(PaymentRecovery.NewPaymentMethod, rejection.Recovery);
    }

    [Fact]
    public async Task ChargeAsync_AuthenticationRequiredDecline_ReturnsOnSessionRecovery()
    {
        stripeClient
            .Setup(c => c.CreatePaymentIntentAsync(It.IsAny<PaymentIntentCreateOptions>(), It.IsAny<RequestOptions?>()))
            .ThrowsAsync(new StripeException("declined")
            {
                StripeError = new StripeError { DeclineCode = "authentication_required" }
            });

        var result = await sut.ChargeAsync(Options());

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentRecovery.OnSessionAuthentication, rejection.Recovery);
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

    [Fact]
    public async Task ChargeAsync_WithOperationId_UsesDurableIdempotencyKey()
    {
        var operationId = Guid.CreateVersion7();
        RequestOptions? request = null;
        stripeClient
            .Setup(c => c.CreatePaymentIntentAsync(
                It.IsAny<PaymentIntentCreateOptions>(),
                It.IsAny<RequestOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<PaymentIntentCreateOptions, RequestOptions?, CancellationToken>((_, value, _) =>
                request = value)
            .ReturnsAsync(new PaymentIntent
            {
                Id = "pi_operation",
                Amount = 1000,
                Status = "succeeded"
            });

        var result = await sut.ChargeAsync(Options(operationId));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            $"financial-operation:{operationId:D}:{operationId:D}:1:charge",
            request?.IdempotencyKey);
    }

    private static StripeChargeOptions Options(Guid? operationId = null) =>
        new()
        {
            OperationId = operationId,
            Amount = Money.Gbp(10),
            PaymentMethodId = "pm_test",
            StripeCustomerId = "cus_test",
            DestinationStripeId = "acct_test",
            ReceiptEmail = "payer@example.com",
            Metadata = new Dictionary<string, string>()
        };
}
