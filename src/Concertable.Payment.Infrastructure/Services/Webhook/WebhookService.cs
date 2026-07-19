using Concertable.Messaging.Contracts;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Application.Commands;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class WebhookService : IWebhookService
{
    private readonly PaymentDbContext context;
    private readonly IDbContextAccessor contextAccessor;
    private readonly IBus bus;
    private readonly string webhookSecret;

    public WebhookService(
        PaymentDbContext context,
        IDbContextAccessor contextAccessor,
        IBus bus,
        IOptions<StripeSettings> stripeSettings)
    {
        this.context = context;
        this.contextAccessor = contextAccessor;
        this.bus = bus;
        webhookSecret = stripeSettings.Value.WebhookSecret
            ?? throw new InvalidOperationException("Stripe:WebhookSecret is not configured — webhook signature validation requires it.");
    }

    public async Task HandleAsync(string json, string stripeSignature)
    {
        EventUtility.ValidateSignature(json, stripeSignature, webhookSecret);

        try
        {
            contextAccessor.Context = context;
            await bus.SendAsync(new ProcessStripeWebhookCommand(json));
            await context.SaveChangesAsync();
        }
        finally
        {
            contextAccessor.Context = null;
        }
    }
}
