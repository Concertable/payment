using Dunet;
using Reunion.Errors;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record PaymentMethodChargeError : IError
{
    public ErrorDefinition Definition => this switch
    {
        PaymentMethodFailure(var error) => error.Definition,
        ChargeFailure(var error) => error.Definition
    };

    public partial record PaymentMethodFailure(PaymentOperationError Error);

    public partial record ChargeFailure(ManagerPaymentOperationError Error);
}
