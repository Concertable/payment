using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class OffSessionConfigurator : IPaymentSessionConfigurator
{
    public void Configure(PaymentIntentCreateOptions options) =>
        options.OffSession = true;
}
