using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class SetupIntentWebhookHandler
{
    private readonly IBus integrationEventBus;
    private readonly ILogger<SetupIntentWebhookHandler> logger;

    public SetupIntentWebhookHandler(
        IBus integrationEventBus,
        ILogger<SetupIntentWebhookHandler> logger)
    {
        this.integrationEventBus = integrationEventBus;
        this.logger = logger;
    }

    public async Task HandleAsync(
        Event stripeEvent,
        SetupIntent intent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Type is not (EventTypes.SetupIntentSucceeded or EventTypes.SetupIntentSetupFailed))
        {
            logger.SkippingStripeEventNotHandled(stripeEvent.Id, stripeEvent.Type);
            return;
        }

        if (intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type) != TransactionTypes.Verify)
            return;

        switch (stripeEvent.Type)
        {
            case EventTypes.SetupIntentSucceeded:
                var enrichedMetadata = new Dictionary<string, string>(intent.Metadata)
                {
                    [PaymentMetadataKeys.PaymentMethodId] = intent.PaymentMethodId
                };
                logger.PublishingVerifyPaymentSucceededEvent(intent.Id, stripeEvent.Id);
                await integrationEventBus.PublishAsync(new PaymentSucceededEvent(intent.Id, enrichedMetadata), cancellationToken);
                break;

            case EventTypes.SetupIntentSetupFailed:
                var error = intent.LastSetupError;
                logger.PublishingVerifyPaymentFailedEvent(intent.Id, stripeEvent.Id, error?.Code, error?.Message);
                await integrationEventBus.PublishAsync(new PaymentFailedEvent(intent.Id, error?.Code, error?.Message, intent.Metadata), cancellationToken);
                break;
        }
    }
}
