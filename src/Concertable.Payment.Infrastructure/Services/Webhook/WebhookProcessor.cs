using Concertable.Messaging.Contracts;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class WebhookProcessor : IWebhookProcessor
{
    private readonly PaymentDbContext context;
    private readonly IStripeEventRepository stripeEventRepository;
    private readonly IBus integrationEventBus;
    private readonly IDbContextAccessor contextAccessor;
    private readonly IStripeHoldClient stripeHoldClient;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<WebhookProcessor> logger;

    public WebhookProcessor(
        PaymentDbContext context,
        IStripeEventRepository stripeEventRepository,
        IBus integrationEventBus,
        IDbContextAccessor contextAccessor,
        IStripeHoldClient stripeHoldClient,
        TimeProvider timeProvider,
        ILogger<WebhookProcessor> logger)
    {
        this.context = context;
        this.stripeEventRepository = stripeEventRepository;
        this.integrationEventBus = integrationEventBus;
        this.contextAccessor = contextAccessor;
        this.stripeHoldClient = stripeHoldClient;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task ProcessAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        try
        {
            logger.ProcessingStripeEvent(stripeEvent.Id, stripeEvent.Type);

            if (stripeEvent.Data.Object is not PaymentIntent intent)
            {
                logger.SkippingStripeEventNotPaymentIntent(stripeEvent.Id, stripeEvent.Data.Object?.GetType().Name ?? "null");
                return;
            }

            if (await stripeEventRepository.EventExistsAsync(stripeEvent.Id))
            {
                logger.SkippingStripeEventAlreadyProcessed(stripeEvent.Id);
                return;
            }

            stripeEventRepository.AddEvent(StripeEventEntity.Create(stripeEvent.Id, timeProvider.GetUtcNow().DateTime));
            contextAccessor.Context = context;

            switch (stripeEvent.Type)
            {
                case EventTypes.PaymentIntentSucceeded:
                    logger.PublishingPaymentSucceededEvent(intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"));
                    await integrationEventBus.PublishAsync(new PaymentSucceededEvent(intent.Id, intent.Metadata), cancellationToken);
                    break;

                case EventTypes.PaymentIntentAmountCapturableUpdated:
                    if (intent.Metadata.TryGetValue(PaymentMetadataKeys.Type, out var capturedType) && capturedType == TransactionTypes.Verify)
                    {
                        logger.CancellingVerifyPaymentIntent(intent.Id, stripeEvent.Id);
                        await stripeHoldClient.CancelAsync(intent.Id, cancellationToken);
                        var enrichedMetadata = new Dictionary<string, string>(intent.Metadata)
                        {
                            [PaymentMetadataKeys.PaymentMethodId] = intent.PaymentMethodId
                        };
                        logger.PublishingVerifyPaymentSucceededEvent(intent.Id, stripeEvent.Id);
                        await integrationEventBus.PublishAsync(new PaymentSucceededEvent(intent.Id, enrichedMetadata), cancellationToken);
                    }
                    break;

                case EventTypes.PaymentIntentPaymentFailed:
                    var failureCode = intent.LastPaymentError?.Code;
                    var failureMessage = intent.LastPaymentError?.Message;
                    logger.PublishingPaymentFailedEvent(intent.Id, stripeEvent.Id, intent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"), failureCode, failureMessage);
                    await integrationEventBus.PublishAsync(new PaymentFailedEvent(intent.Id, failureCode, failureMessage, intent.Metadata), cancellationToken);
                    break;

                default:
                    logger.SkippingStripeEventNotHandled(stripeEvent.Id, stripeEvent.Type);
                    break;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.StripeWebhookProcessingError(stripeEvent.Id, ex);
            throw;
        }
        finally
        {
            contextAccessor.Context = null;
        }
    }
}
