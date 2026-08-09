using Reunion.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record PaymentError : IError
{
    public ErrorDefinition Definition => this switch
    {
        PayerNotFound => ErrorDefinition.NotFound<PayerNotFound>("The payer account was not found."),
        PayeeNotFound => ErrorDefinition.NotFound<PayeeNotFound>("The payee account was not found."),
        PayerUnavailable => ErrorDefinition.Conflict<PayerUnavailable>("The payer account is not ready for payments."),
        RecipientUnavailable => ErrorDefinition.Conflict<RecipientUnavailable>("The recipient account is not ready for payments."),
        PaymentRejected => ErrorDefinition.PaymentRequired<PaymentRejected>("The payment was rejected."),
        CommissionFailure(var error) => error.Definition
    };

    public partial record PayerNotFound;

    public partial record PayeeNotFound;

    public partial record PayerUnavailable;

    public partial record RecipientUnavailable;

    public partial record PaymentRejected;

    public partial record CommissionFailure(CommissionError Error);
}
