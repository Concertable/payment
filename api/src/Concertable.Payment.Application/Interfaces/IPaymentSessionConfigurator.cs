using Stripe;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionConfigurator
{
    void Configure(PaymentIntentCreateOptions options);
}
