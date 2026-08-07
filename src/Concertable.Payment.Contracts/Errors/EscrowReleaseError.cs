using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowReleaseError : IError
{
    public ErrorDefinition Definition => this switch
    {
        EscrowNotFound => ErrorDefinition.NotFound<EscrowNotFound>(),
        EscrowNotHeld => ErrorDefinition.Conflict<EscrowNotHeld>("Only held escrow can be released."),
        PaymentFailure(var error) => error.Definition
    };

    public partial record EscrowNotFound;

    public partial record EscrowNotHeld;

    public partial record PaymentFailure(PaymentError Error);
}
