using Dunet;
using Reunion.Errors;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record PaymentMethodChargeError : IError
{
    public ErrorDefinition Definition => this switch
    {
        PaymentMethodFailure(var error) => error.Definition,
        ChargeFailure(var error) => error.Definition,
        AuthenticationRequired => ErrorDefinition.PaymentRequired<AuthenticationRequired>(
            "The committed payment method requires the payer to authenticate on-session before the charge can complete.")
    };

    public partial record PaymentMethodFailure(PaymentOperationError Error);

    public partial record ChargeFailure(ManagerPaymentOperationError Error);

    [ErrorCode("payment.charge.authentication_required")]
    public partial record AuthenticationRequired;
}
