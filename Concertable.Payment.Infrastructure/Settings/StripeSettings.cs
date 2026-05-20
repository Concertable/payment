namespace Concertable.Payment.Infrastructure.Settings;

internal class StripeSettings
{
    public string? SecretKey { get; set; }
    public string? PublishableKey { get; set; }
    public string? WebhookSecret { get; set; }
    public bool SkipWebhookVerification { get; set; }
}
