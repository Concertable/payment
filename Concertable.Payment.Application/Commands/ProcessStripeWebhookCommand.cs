using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Application.Commands;

[MessageType("concertable.payment.process-stripe-webhook.v1")]
public sealed record ProcessStripeWebhookCommand(string EventJson) : IIntegrationCommand;
