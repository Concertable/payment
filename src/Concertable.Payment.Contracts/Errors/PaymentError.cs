using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record PaymentError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record PayerNotFound
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.NotFound("payment.payer_not_found", "The payer account was not found.");
    }

    public partial record PayeeNotFound
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.NotFound("payment.payee_not_found", "The payee account was not found.");
    }

    public partial record PayerUnavailable
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("payment.payer_unavailable", "The payer account is not ready for payments.");
    }

    public partial record RecipientUnavailable
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("payment.recipient_unavailable", "The recipient account is not ready for payments.");
    }

    public partial record PaymentRejected
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.PaymentRequired("payment.rejected", "The payment was rejected.");
    }

    public partial record CommissionFailure(CommissionError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }
}
