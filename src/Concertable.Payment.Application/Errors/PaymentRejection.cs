namespace Concertable.Payment.Application.Errors;

internal readonly record struct PaymentRejection
{
    public required PaymentError Error { get; init; }
    public required PaymentRecovery Recovery { get; init; }

    public static PaymentRejection Declined(PaymentError error) =>
        new() { Error = error, Recovery = PaymentRecovery.NewPaymentMethod };

    public static PaymentRejection Unrecoverable(PaymentError error) =>
        new() { Error = error, Recovery = PaymentRecovery.None };
}
