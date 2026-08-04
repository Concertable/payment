using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowReleaseError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record EscrowNotFound
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.NotFound("escrow.release_not_found", "Escrow not found.");
    }

    public partial record EscrowNotHeld
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("escrow.release_not_held", "Only held escrow can be released.");
    }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<EscrowReleaseError> FromCode(string code) => code switch
    {
        "escrow.release_not_found" => Option.Some<EscrowReleaseError>(new EscrowNotFound()),
        "escrow.release_not_held" => Option.Some<EscrowReleaseError>(new EscrowNotHeld()),
        _ => PaymentError.FromCode(code).Match(
            payment => Option.Some<EscrowReleaseError>(new PaymentFailure(payment)),
            Option.None<EscrowReleaseError>)
    };
}
