namespace Concertable.Payment.Application.Errors;

internal static class ChargeErrorMappers
{
    extension(ChargeError error)
    {
        public PaymentError ToPaymentError() => error switch
        {
            ChargeError.AuthenticationRequired => new PaymentError.PaymentRejected(),
            ChargeError.PaymentFailure(var payment) => payment
        };

        public PaymentMethodChargeError ToPaymentMethodChargeError() => error switch
        {
            ChargeError.AuthenticationRequired => new PaymentMethodChargeError.AuthenticationRequired(),
            ChargeError.PaymentFailure(var payment) => new PaymentMethodChargeError.PaymentFailure(payment)
        };
    }
}
