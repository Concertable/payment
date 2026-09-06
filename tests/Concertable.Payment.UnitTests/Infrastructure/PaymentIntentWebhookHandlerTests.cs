using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentIntentWebhookHandlerTests
{
    [Theory]
    [InlineData(EventTypes.PaymentIntentCreated)]
    [InlineData(EventTypes.PaymentIntentSucceeded)]
    [InlineData(EventTypes.PaymentIntentPaymentFailed)]
    public async Task HandleAsync_EventWithoutReferenceMetadata_SkipsEvent(string eventType)
    {
        var bus = new Mock<IBus>();
        var sut = new PaymentIntentWebhookHandler(
            bus.Object,
            NullLogger<PaymentIntentWebhookHandler>.Instance);

        await sut.HandleAsync(
            new Event { Id = "evt_test", Type = eventType },
            new PaymentIntent { Id = "pi_test", Metadata = [] },
            CancellationToken.None);

        bus.VerifyNoOtherCalls();
    }
}
