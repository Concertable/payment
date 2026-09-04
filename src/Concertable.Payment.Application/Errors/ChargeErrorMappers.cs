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

        public ManagerChargeError ToManagerChargeError() => error switch
        {
            ChargeError.AuthenticationRequired => new ManagerChargeError.AuthenticationRequired(),
            ChargeError.PaymentFailure(var payment) => new ManagerChargeError.OperationFailure(
                new ManagerPaymentOperationError.ManagerFailure(
                    new ManagerPaymentError.PaymentFailure(payment)))
        };
    }

    extension(ManagerChargeError error)
    {
        public ManagerPaymentOperationError ToOperationError() => error switch
        {
            ManagerChargeError.AuthenticationRequired => new ManagerPaymentOperationError.ManagerFailure(
                new ManagerPaymentError.PaymentFailure(new PaymentError.PaymentRejected())),
            ManagerChargeError.OperationFailure(var operation) => operation
        };
    }
}
