using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal class WebhookService : IWebhookService
{
    private readonly IWebhookQueue webhookQueue;
    private readonly string webhookSecret;
    private readonly bool skipVerification;

    public WebhookService(IWebhookQueue webhookQueue, IOptions<StripeSettings> stripeSettings)
    {
        this.webhookQueue = webhookQueue;
        webhookSecret = stripeSettings.Value.WebhookSecret ?? string.Empty;
        skipVerification = stripeSettings.Value.SkipWebhookVerification;
    }

    public async Task HandleAsync(string json, string stripeSignature)
    {
        var stripeEvent = skipVerification
            ? EventUtility.ParseEvent(json)
            : EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        await webhookQueue.EnqueueAsync(stripeEvent);
    }
}
