using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeWebhookService : IWebhookService
{
    private readonly IWebhookQueue webhookQueue;

    public FakeWebhookService(IWebhookQueue webhookQueue)
    {
        this.webhookQueue = webhookQueue;
    }

    public Task HandleAsync(string json, string stripeSignature)
    {
        var stripeEvent = EventUtility.ParseEvent(json);
        return webhookQueue.EnqueueAsync(stripeEvent);
    }
}
