using Reunion.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record ManagerPaymentOperationError : IError
{
    public ErrorDefinition Definition => this switch
    {
        ManagerFailure(var error) => error.Definition,
        OperationConflict => ErrorDefinition.Conflict<OperationConflict>(
            "The operation identity conflicts with an existing manager payment.")
    };

    public partial record ManagerFailure(ManagerPaymentError Error);

    [ErrorCode("payment.manager_operation_conflict")]
    public partial record OperationConflict;
}
