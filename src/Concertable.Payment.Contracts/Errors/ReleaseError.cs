using Concertable.Kernel.Errors;
namespace Concertable.Payment.Contracts.Errors;

public sealed record ReleaseError(ErrorDefinition Definition) : IError
{
    public static readonly ReleaseError EscrowNotFound = new(
        ErrorDefinition.NotFound("payment.escrow_not_found", "The escrow payment was not found."));

    public static readonly ReleaseError InvalidEscrowState = new(
        ErrorDefinition.Conflict("payment.escrow_release_invalid_state", "The escrow payment cannot be released in its current state."));

    public static readonly ReleaseError RecipientUnavailable = new(
        ErrorDefinition.Conflict("payment.recipient_unavailable", "The recipient account is not ready for payments."));

    public static readonly ReleaseError ReleaseRejected = new(
        ErrorDefinition.Invalid("payment.escrow_release_rejected", "The escrow release was rejected."));
}
