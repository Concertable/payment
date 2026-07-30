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
    private readonly TimeProvider timeProvider;
    private readonly ILogger<WebhookProcessor> logger;

    public WebhookProcessor(
        PaymentDbContext context,
        IStripeEventRepository stripeEventRepository,
        IBus integrationEventBus,
        IDbContextAccessor contextAccessor,
        TimeProvider timeProvider,
        ILogger<WebhookProcessor> logger)
    {
        this.context = context;
        this.stripeEventRepository = stripeEventRepository;
        this.integrationEventBus = integrationEventBus;
        this.contextAccessor = contextAccessor;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task ProcessAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        try
        {
            logger.ProcessingStripeEvent(stripeEvent.Id, stripeEvent.Type);

            var dataObject = stripeEvent.Data.Object;
            if (dataObject is not (PaymentIntent or SetupIntent))
            {
                logger.SkippingStripeEventUnhandledObject(stripeEvent.Id, dataObject?.GetType().Name ?? "null");
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
                    var succeededIntent = (PaymentIntent)dataObject;
                    logger.PublishingPaymentSucceededEvent(succeededIntent.Id, stripeEvent.Id, succeededIntent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"));
                    await integrationEventBus.PublishAsync(new PaymentSucceededEvent(succeededIntent.Id, succeededIntent.Metadata), cancellationToken);
                    break;

                case EventTypes.PaymentIntentPaymentFailed:
                    var failedIntent = (PaymentIntent)dataObject;
                    var failureCode = failedIntent.LastPaymentError?.Code;
                    var failureMessage = failedIntent.LastPaymentError?.Message;
                    logger.PublishingPaymentFailedEvent(failedIntent.Id, stripeEvent.Id, failedIntent.Metadata.GetValueOrDefault(PaymentMetadataKeys.Type, "unknown"), failureCode, failureMessage);
                    await integrationEventBus.PublishAsync(new PaymentFailedEvent(failedIntent.Id, failureCode, failureMessage, failedIntent.Metadata), cancellationToken);
                    break;

                case EventTypes.SetupIntentSucceeded:
                    var setupIntent = (SetupIntent)dataObject;
                    if (setupIntent.Metadata.TryGetValue(PaymentMetadataKeys.Type, out var verifyType) && verifyType == TransactionTypes.Verify)
                    {
                        var enrichedMetadata = new Dictionary<string, string>(setupIntent.Metadata)
                        {
                            [PaymentMetadataKeys.PaymentMethodId] = setupIntent.PaymentMethodId
                        };
                        logger.PublishingVerifyPaymentSucceededEvent(setupIntent.Id, stripeEvent.Id);
                        await integrationEventBus.PublishAsync(new PaymentSucceededEvent(setupIntent.Id, enrichedMetadata), cancellationToken);
                    }
                    break;

                case EventTypes.SetupIntentSetupFailed:
                    var failedSetup = (SetupIntent)dataObject;
                    if (failedSetup.Metadata.TryGetValue(PaymentMetadataKeys.Type, out var failedType) && failedType == TransactionTypes.Verify)
                    {
                        var setupFailureCode = failedSetup.LastSetupError?.Code;
                        var setupFailureMessage = failedSetup.LastSetupError?.Message;
                        logger.PublishingVerifyPaymentFailedEvent(failedSetup.Id, stripeEvent.Id, setupFailureCode, setupFailureMessage);
                        await integrationEventBus.PublishAsync(new PaymentFailedEvent(failedSetup.Id, setupFailureCode, setupFailureMessage, failedSetup.Metadata), cancellationToken);
                    }
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
