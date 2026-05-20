namespace Concertable.Payment.Application.Interfaces;

internal interface IStripePaymentIntentClientFactory
{
    IStripePaymentIntentClient Create(PaymentSession session);
}
