namespace Concertable.Payment.Application.Errors;

internal readonly record struct ManagerPaymentRejection
{
    public required ManagerPaymentOperationError Error { get; init; }
    public required PaymentRecovery Recovery { get; init; }

    public static ManagerPaymentRejection Unrecoverable(ManagerPaymentOperationError error) =>
        new() { Error = error, Recovery = PaymentRecovery.None };

    public static ManagerPaymentRejection FromPayment(PaymentRejection rejection) =>
        new()
        {
            Error = new ManagerPaymentOperationError.ManagerFailure(
                new ManagerPaymentError.PaymentFailure(rejection.Error)),
            Recovery = rejection.Recovery
        };
}
