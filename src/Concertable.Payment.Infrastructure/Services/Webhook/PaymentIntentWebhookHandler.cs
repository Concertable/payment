using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class PaymentIntentWebhookHandler : IStripeWebhookHandler<PaymentIntent>
{
    private readonly IBus integrationEventBus;
    private readonly ILogger<PaymentIntentWebhookHandler> logger;

    public PaymentIntentWebhookHandler(
        IBus integrationEventBus,
        ILogger<PaymentIntentWebhookHandler> logger)
    {
        this.integrationEventBus = integrationEventBus;
        this.logger = logger;
    }

    public async Task HandleAsync(
        Event stripeEvent,
        PaymentIntent intent,
        CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case EventTypes.PaymentIntentSucceeded:
                logger.PublishingPaymentSucceededEvent(intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"));
                await integrationEventBus.PublishAsync(new PaymentSucceededEvent(intent.Id, intent.Metadata), cancellationToken);
                break;

            case EventTypes.PaymentIntentPaymentFailed:
                var error = intent.LastPaymentError;
                logger.PublishingPaymentFailedEvent(intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"), error?.Code, error?.Message);
                await integrationEventBus.PublishAsync(new PaymentFailedEvent(intent.Id, error?.Code, error?.Message, intent.Metadata), cancellationToken);
                break;

            default:
                logger.SkippingStripeEventNotHandled(stripeEvent.Id, stripeEvent.Type);
                break;
        }
    }
}
