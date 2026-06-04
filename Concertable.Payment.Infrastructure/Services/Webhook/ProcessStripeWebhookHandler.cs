using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Commands;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal sealed class ProcessStripeWebhookHandler : IIntegrationCommandHandler<ProcessStripeWebhookCommand>
{
    private readonly IWebhookProcessor webhookProcessor;

    public ProcessStripeWebhookHandler(IWebhookProcessor webhookProcessor)
    {
        this.webhookProcessor = webhookProcessor;
    }

    public Task HandleAsync(ProcessStripeWebhookCommand command, MessageEnvelope envelope, CancellationToken ct = default) =>
        webhookProcessor.ProcessAsync(EventUtility.ParseEvent(command.EventJson), ct);
}
