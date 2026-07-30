using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class WebhookProcessor : IWebhookProcessor
{
    private readonly IStripeEventRepository stripeEventRepository;
    private readonly IOutboxUnitOfWorkBehavior outboxBehavior;
    private readonly TimeProvider timeProvider;
    private readonly PaymentIntentWebhookHandler paymentIntentHandler;
    private readonly SetupIntentWebhookHandler setupIntentHandler;
    private readonly ILogger<WebhookProcessor> logger;

    public WebhookProcessor(
        IStripeEventRepository stripeEventRepository,
        IOutboxUnitOfWorkBehavior outboxBehavior,
        TimeProvider timeProvider,
        PaymentIntentWebhookHandler paymentIntentHandler,
        SetupIntentWebhookHandler setupIntentHandler,
        ILogger<WebhookProcessor> logger)
    {
        this.stripeEventRepository = stripeEventRepository;
        this.outboxBehavior = outboxBehavior;
        this.timeProvider = timeProvider;
        this.paymentIntentHandler = paymentIntentHandler;
        this.setupIntentHandler = setupIntentHandler;
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

            await outboxBehavior.ExecuteAsync(async () =>
            {
                stripeEventRepository.AddEvent(StripeEventEntity.Create(stripeEvent.Id, timeProvider.GetUtcNow().DateTime));

                switch (dataObject)
                {
                    case PaymentIntent intent:
                        await paymentIntentHandler.HandleAsync(stripeEvent, intent, cancellationToken);
                        break;

                    case SetupIntent intent:
                        await setupIntentHandler.HandleAsync(stripeEvent, intent, cancellationToken);
                        break;
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.StripeWebhookProcessingError(stripeEvent.Id, ex);
            throw;
        }
    }
}
