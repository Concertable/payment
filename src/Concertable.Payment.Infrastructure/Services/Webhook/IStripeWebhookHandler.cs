using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal interface IStripeWebhookHandler<TIntent>
{
    Task HandleAsync(
        Event stripeEvent,
        TIntent intent,
        CancellationToken cancellationToken);
}
