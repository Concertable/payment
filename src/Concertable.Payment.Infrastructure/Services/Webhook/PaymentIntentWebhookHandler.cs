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
                if (!TryGetReference(intent, out var succeededReference))
                {
                    logger.SkippingStripeEventWithoutOperationReference(stripeEvent.Id, stripeEvent.Type);
                    return;
                }
                logger.PublishingPaymentSucceededEvent(intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"));
                await integrationEventBus.PublishAsync(new PaymentSucceededEvent(succeededReference, intent.Metadata), cancellationToken);
                break;

            case EventTypes.PaymentIntentPaymentFailed:
                if (!TryGetReference(intent, out var failedReference))
                {
                    logger.SkippingStripeEventWithoutOperationReference(stripeEvent.Id, stripeEvent.Type);
                    return;
                }
                var error = intent.LastPaymentError;
                logger.PublishingPaymentFailedEvent(intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"), error?.Code, error?.Message);
                await integrationEventBus.PublishAsync(new PaymentFailedEvent(failedReference, error?.Code, error?.Message, intent.Metadata), cancellationToken);
                break;

            default:
                logger.SkippingStripeEventNotHandled(stripeEvent.Id, stripeEvent.Type);
                break;
        }
    }

    private static bool TryGetReference(
        PaymentIntent intent,
        out PaymentOperationReference reference)
    {
        if (intent.Metadata.TryGetValue(PaymentMetadataKeys.OperationType, out var operationType)
            && intent.Metadata.TryGetValue(PaymentMetadataKeys.ClientReference, out var clientReference))
        {
            reference = new(operationType, clientReference);
            return true;
        }

        reference = default;
        return false;
    }
}
