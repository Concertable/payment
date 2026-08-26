using Reunion.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowReleaseOperationError : IError
{
    public ErrorDefinition Definition => this switch
    {
        ReleaseFailure(var error) => error.Definition,
        OperationConflict => ErrorDefinition.Conflict<OperationConflict>(
            "The operation identity conflicts with the escrow release.")
    };

    public partial record ReleaseFailure(EscrowReleaseError Error);

    [ErrorCode("escrow.release_operation_conflict")]
    public partial record OperationConflict;
}
