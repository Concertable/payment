using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal interface IStripeEventFilter
{
    bool ShouldProcess(Event stripeEvent);
}

internal sealed class AcceptAllStripeEventFilter : IStripeEventFilter
{
    public bool ShouldProcess(Event stripeEvent) => true;
}
