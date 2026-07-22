using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Commands;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class WebhookService : IWebhookService
{
    private readonly IOutboxUnitOfWorkBehavior outboxBehavior;
    private readonly IBus bus;
    private readonly string webhookSecret;

    public WebhookService(
        IOutboxUnitOfWorkBehavior outboxBehavior,
        IBus bus,
        IOptions<StripeSettings> stripeSettings)
    {
        this.outboxBehavior = outboxBehavior;
        this.bus = bus;
        webhookSecret = stripeSettings.Value.WebhookSecret
            ?? throw new InvalidOperationException("Stripe:WebhookSecret is not configured — webhook signature validation requires it.");
    }

    public Task HandleAsync(string json, string stripeSignature)
    {
        EventUtility.ValidateSignature(json, stripeSignature, webhookSecret);

        return outboxBehavior.ExecuteAsync(() => bus.SendAsync(new ProcessStripeWebhookCommand(json)));
    }
}
