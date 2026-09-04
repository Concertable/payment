using Dunet;
using Reunion.Errors;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record PaymentMethodChargeError : IError
{
    public ErrorDefinition Definition => this switch
    {
        PaymentMethodFailure(var error) => error.Definition,
        PaymentFailure(var error) => error.Definition,
        CommissionFailure(var error) => error.Definition,
        OperationConflict => ErrorDefinition.Conflict<OperationConflict>(
            "The operation identity conflicts with an existing payment."),
        AuthenticationRequired => ErrorDefinition.PaymentRequired<AuthenticationRequired>(
            "The committed payment method requires the payer to authenticate on-session before the charge can complete.")
    };

    public partial record PaymentMethodFailure(PaymentOperationError Error);

    public partial record PaymentFailure(PaymentError Error);

    public partial record CommissionFailure(CommissionError Error);

    [ErrorCode("payment.operation_conflict")]
    public partial record OperationConflict;

    [ErrorCode("payment.charge.authentication_required")]
    public partial record AuthenticationRequired;
}
