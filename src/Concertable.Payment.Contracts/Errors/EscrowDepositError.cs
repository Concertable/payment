using Reunion.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowDepositError : IError
{
    public ErrorDefinition Definition => this switch
    {
        PaymentFailure(var error) => error.Definition,
        CommissionFailure(var error) => error.Definition,
        PaymentOperationFailure(var error) => error.Definition
    };

    public partial record PaymentFailure(PaymentError Error);

    public partial record CommissionFailure(CommissionError Error);

    public partial record PaymentOperationFailure(PaymentOperationError Error);
}
