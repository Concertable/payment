using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class OnSessionConfigurator : IPaymentSessionConfigurator
{
    public void Configure(PaymentIntentCreateOptions options)
    {
        options.ConfirmationMethod = "automatic";
        options.CaptureMethod = "automatic";
    }
}
