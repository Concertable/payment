using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union]
public partial record ReleaseError : IError
{
    partial record EscrowNotFound;
    partial record InvalidEscrowState;
    partial record RecipientUnavailable;
    partial record ReleaseRejected;

    public static ReleaseError NotFound() => new EscrowNotFound();
    public static ReleaseError InvalidState() => new InvalidEscrowState();
    public static ReleaseError UnavailableRecipient() => new RecipientUnavailable();
    public static ReleaseError Rejected() => new ReleaseRejected();

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        escrowNotFound => ErrorDefinition.NotFound("payment.escrow_not_found", "The escrow payment was not found."),
        invalidEscrowState => ErrorDefinition.Conflict("payment.escrow_release_invalid_state", "The escrow payment cannot be released in its current state."),
        recipientUnavailable => ErrorDefinition.Conflict("payment.recipient_unavailable", "The recipient account is not ready for payments."),
        releaseRejected => ErrorDefinition.Invalid("payment.escrow_release_rejected", "The escrow release was rejected."));
}
