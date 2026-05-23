using Concertable.Messaging.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal class WebhookProcessor : IWebhookProcessor
{
    private readonly IStripeEventRepository stripeEventRepository;
    private readonly IBus integrationEventBus;
    private readonly IStripeHoldClient stripeHoldClient;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<WebhookProcessor> logger;

    public WebhookProcessor(
        IStripeEventRepository stripeEventRepository,
        [FromKeyedServices("webhook")] IBus integrationEventBus,
        IStripeHoldClient stripeHoldClient,
        TimeProvider timeProvider,
        ILogger<WebhookProcessor> logger)
    {
        this.stripeEventRepository = stripeEventRepository;
        this.integrationEventBus = integrationEventBus;
        this.stripeHoldClient = stripeHoldClient;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task ProcessAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Processing Stripe event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);

            if (stripeEvent.Data.Object is not PaymentIntent intent)
            {
                logger.LogInformation(
                    "Skipping Stripe event {EventId}: data object is {ObjectType}, not PaymentIntent",
                    stripeEvent.Id, stripeEvent.Data.Object?.GetType().Name ?? "null");
                return;
            }

            if (await stripeEventRepository.EventExistsAsync(stripeEvent.Id))
            {
                logger.LogInformation(
                    "Skipping Stripe event {EventId}: already processed",
                    stripeEvent.Id);
                return;
            }

            await stripeEventRepository.AddEventAsync(StripeEventEntity.Create(stripeEvent.Id, timeProvider.GetUtcNow().DateTime));

            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    logger.LogInformation(
                        "Publishing PaymentSucceededEvent for PaymentIntent {IntentId} (event {EventId}) of transaction type {TransactionType}",
                        intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault("type", "unknown"));
                    await integrationEventBus.PublishAsync(new PaymentSucceededEvent(intent.Id, intent.Metadata), cancellationToken);
                    break;

                case "payment_intent.amount_capturable_updated":
                    if (intent.Metadata.TryGetValue("type", out var capturedType) && capturedType == TransactionTypes.Verify)
                    {
                        logger.LogInformation(
                            "Cancelling verify PaymentIntent {IntentId} after 3DS completion (event {EventId})",
                            intent.Id, stripeEvent.Id);
                        await stripeHoldClient.CancelAsync(intent.Id, cancellationToken);
                        var enrichedMetadata = new Dictionary<string, string>(intent.Metadata)
                        {
                            ["paymentMethodId"] = intent.PaymentMethodId
                        };
                        logger.LogInformation(
                            "Publishing PaymentSucceededEvent for verify PaymentIntent {IntentId} (event {EventId})",
                            intent.Id, stripeEvent.Id);
                        await integrationEventBus.PublishAsync(new PaymentSucceededEvent(intent.Id, enrichedMetadata), cancellationToken);
                    }
                    break;

                case "payment_intent.payment_failed":
                    var failureCode = intent.LastPaymentError?.Code;
                    var failureMessage = intent.LastPaymentError?.Message;
                    logger.LogInformation(
                        "Publishing PaymentFailedEvent for PaymentIntent {IntentId} (event {EventId}) of transaction type {TransactionType}: {Code} {Message}",
                        intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault("type", "unknown"), failureCode, failureMessage);
                    await integrationEventBus.PublishAsync(new PaymentFailedEvent(intent.Id, failureCode, failureMessage, intent.Metadata), cancellationToken);
                    break;

                default:
                    logger.LogInformation(
                        "Skipping Stripe event {EventId}: type {EventType} not handled",
                        stripeEvent.Id, stripeEvent.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Stripe webhook for event {EventId}", stripeEvent.Id);
        }
    }
}
