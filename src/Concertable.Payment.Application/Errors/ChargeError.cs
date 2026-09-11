using Dunet;
using Reunion.Errors;

namespace Concertable.Payment.Application.Errors;

[Union(EnableImplicitConversions = false)]
internal abstract partial record ChargeError : IError
{
    public ErrorDefinition Definition => this switch
    {
        AuthenticationRequired => ErrorDefinition.PaymentRequired<AuthenticationRequired>(
            "The payment method requires the payer to authenticate on-session before the charge can complete."),
        PaymentFailure(var error) => error.Definition
    };

    public partial record AuthenticationRequired;

    public partial record PaymentFailure(PaymentError Error);
}
