namespace Concertable.Payment.Client.Enums;

/// <summary>Onboarding/verification state of an owner's Stripe connect account, as seen by a consumer fronting
/// the payout endpoints. Member names are the wire contract (serialized as strings) shared with the SPAs.</summary>
public enum PayoutAccountStatus
{
    NotVerified,
    Pending,
    Verified
}
