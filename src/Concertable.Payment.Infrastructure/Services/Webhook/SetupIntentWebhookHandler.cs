using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class SetupIntentWebhookHandler : IStripeWebhookHandler<SetupIntent>
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

        var reference = new PaymentOperationReference(
            intent.Metadata.GetValue(PaymentMetadataKeys.OperationType),
            intent.Metadata.GetValue(PaymentMetadataKeys.ClientReference));

        switch (stripeEvent.Type)
        {
            case EventTypes.SetupIntentSucceeded:
                logger.PublishingVerifyPaymentSucceededEvent(intent.Id, stripeEvent.Id);
                await integrationEventBus.PublishAsync(
                    new PaymentSucceededEvent(reference, intent.Metadata),
                    cancellationToken);
                break;

            case EventTypes.SetupIntentSetupFailed:
                var error = intent.LastSetupError;
                logger.PublishingVerifyPaymentFailedEvent(intent.Id, stripeEvent.Id, error?.Code, error?.Message);
                await integrationEventBus.PublishAsync(new PaymentFailedEvent(reference, error?.Code, error?.Message, intent.Metadata), cancellationToken);
                break;
        }
    }
}
