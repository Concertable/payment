namespace Concertable.Payment.Application.Enums;

/// <summary>Onboarding/verification state of an owner's Stripe connect account as surfaced to consumers of the
/// payout endpoints. This is the boundary contract (HTTP <c>account-status</c> serialized as strings, and the
/// source the gRPC proxy maps to proto) — kept distinct from the internal <c>Domain.Enums.PayoutAccountStatus</c>.</summary>
public enum PayoutAccountStatus
{
    NotVerified,
    Pending,
    Verified
}
