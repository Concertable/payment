namespace Concertable.Payment.Application.Interfaces;

/// <summary>The payout-account operations a consumer fronts for its own users, keyed on the opaque owner id
/// (Customer's HTTP controller passes the caller's own owner claim; B2B's gRPC proxy passes the tenant id).
/// Null returns are the "no connect account / no payout account" cases the boundary maps to its own status code.</summary>
internal interface IPayoutAccountService
{
    /// <summary>The Stripe-hosted onboarding URL, or null when the owner has no connect account.</summary>
    Task<string?> GetOnboardingLinkAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>Onboarding/verification status — <see cref="PayoutAccountStatus.NotVerified"/> when there's no account.</summary>
    Task<PayoutAccountStatus> GetAccountStatusAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>The owner's default saved card, or null when there's no Stripe customer.</summary>
    Task<PaymentMethodDto?> GetPaymentMethodAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>A SetupIntent client secret for the card-save flow (provisioning the customer first if needed),
    /// or null when the owner has no payout account at all.</summary>
    Task<string?> CreateSetupIntentAsync(Guid ownerId, CancellationToken ct = default);
}
