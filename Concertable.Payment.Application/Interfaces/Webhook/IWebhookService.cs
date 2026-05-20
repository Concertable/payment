namespace Concertable.Payment.Application.Interfaces.Webhook;

internal interface IWebhookService
{
    Task HandleAsync(string json, string stripeSignature);
}
