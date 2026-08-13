using Concertable.Payment.Application.Interfaces.Webhook;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.E2ETests.Stripe;

internal sealed class StripeWebhookProcessor : IWebhookProcessor
{
    private readonly IWebhookProcessor inner;
    private readonly StripeAccountResolver resolver;
    private readonly ILogger<StripeWebhookProcessor> logger;

    public StripeWebhookProcessor(
        IWebhookProcessor inner,
        StripeAccountResolver resolver,
        ILogger<StripeWebhookProcessor> logger)
    {
        this.inner = inner;
        this.resolver = resolver;
        this.logger = logger;
    }

    public Task ProcessAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var customerId = stripeEvent.Data.Object switch
        {
            PaymentIntent intent => intent.CustomerId,
            SetupIntent intent => intent.CustomerId,
            _ => null,
        };

        if (stripeEvent.Data.Object is PaymentIntent or SetupIntent && !resolver.OwnsCustomer(customerId))
        {
            logger.SkippingStripeEventOutsideE2ERun(stripeEvent.Id);
            return Task.CompletedTask;
        }

        return inner.ProcessAsync(stripeEvent, cancellationToken);
    }
}
