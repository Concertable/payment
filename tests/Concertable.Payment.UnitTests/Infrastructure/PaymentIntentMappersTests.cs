using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Mappers;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentIntentMappersTests
{
    [Fact]
    public void ToPaymentResult_Succeeded_MapsOutcome()
    {
        var intent = new PaymentIntent
        {
            Id = "pi_test",
            Status = StripePaymentIntentStatuses.Succeeded,
            ClientSecret = "secret"
        };

        var result = intent.ToPaymentResult();

        Assert.True(result.TryGetValue(out var outcome));
        Assert.False(outcome.RequiresAction);
        Assert.Equal("pi_test", outcome.ProviderTransactionId);
        Assert.Equal("secret", outcome.ClientSecret);
    }

    [Theory]
    [InlineData(StripePaymentIntentStatuses.RequiresAction)]
    [InlineData(StripePaymentIntentStatuses.RequiresConfirmation)]
    public void ToPaymentResult_RequiresFurtherAction_MapsRequiresAction(string status)
    {
        var intent = new PaymentIntent { Id = "pi_test", Status = status };

        var result = intent.ToPaymentResult();

        Assert.True(result.TryGetValue(out var outcome));
        Assert.True(outcome.RequiresAction);
    }

    [Fact]
    public void ToPaymentResult_RejectedStatus_ReturnsPaymentRejected()
    {
        var intent = new PaymentIntent { Id = "pi_test", Status = "canceled" };

        var result = intent.ToPaymentResult();

        Assert.True(result.TryGetError(out var error));
        Assert.IsType<PaymentError.PaymentRejected>(error);
    }

    [Fact]
    public void ToPaymentResult_MissingId_Throws()
    {
        var intent = new PaymentIntent { Id = null!, Status = StripePaymentIntentStatuses.Succeeded };

        Assert.Throws<InvalidOperationException>(() => intent.ToPaymentResult());
    }
}
