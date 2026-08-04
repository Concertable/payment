using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union]
public partial record PaymentError : IError
{
    partial record PayerNotFound;
    partial record PayeeNotFound;
    partial record PayerUnavailable;
    partial record RecipientUnavailable;
    partial record PaymentRejected;
    partial record CommissionFailure(CommissionError Error);

    public static PaymentError MissingPayer() => new PayerNotFound();
    public static PaymentError MissingPayee() => new PayeeNotFound();
    public static PaymentError UnavailablePayer() => new PayerUnavailable();
    public static PaymentError UnavailableRecipient() => new RecipientUnavailable();
    public static PaymentError Rejected() => new PaymentRejected();
    public static PaymentError Commission(CommissionError error) => new CommissionFailure(error);

    public ErrorDefinition Definition => Match(
        payerNotFound => ErrorDefinition.NotFound("payment.payer_not_found", "The payer account was not found."),
        payeeNotFound => ErrorDefinition.NotFound("payment.payee_not_found", "The payee account was not found."),
        payerUnavailable => ErrorDefinition.Conflict("payment.payer_unavailable", "The payer account is not ready for payments."),
        recipientUnavailable => ErrorDefinition.Conflict("payment.recipient_unavailable", "The recipient account is not ready for payments."),
        paymentRejected => ErrorDefinition.PaymentRequired("payment.rejected", "The payment was rejected."),
        commissionFailure => commissionFailure.Error.Definition);
}
