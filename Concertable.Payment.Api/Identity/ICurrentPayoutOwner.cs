namespace Concertable.Payment.Api.Identity;

/// <summary>
/// Resolves the opaque payout-account owner key for the calling principal, read from the <c>owner</c> claim
/// at Payment's HTTP boundary. Every authenticated principal carries one — each service's
/// <c>UserClaimsController</c> decides what it means (B2B issues its tenant id; Customer issues the user's own
/// id). An absent claim is an invalid principal for an owner-scoped operation and fails closed.
/// </summary>
internal interface ICurrentPayoutOwner
{
    Guid OwnerId { get; }
}
