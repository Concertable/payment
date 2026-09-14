using Stripe;

namespace Concertable.Payment.Application.Interfaces.Webhook;

internal interface IWebhookQueue
{
    Task EnqueueAsync(Event stripeEvent);
}
