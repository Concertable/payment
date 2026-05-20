using Stripe;

namespace Concertable.Payment.Application.Interfaces.Webhook;

internal interface IWebhookProcessor
{
    Task ProcessAsync(Event stripeEvent, CancellationToken cancellationToken);
}
