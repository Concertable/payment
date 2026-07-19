using Concertable.Payment.Client.Enums;

namespace Concertable.Payment.Client;

/// <summary>gRPC client for the owner-keyed payout-account operations a consumer fronts for its own users. The
/// owner id is the consumer's opaque key (B2B passes the active tenant id). A null result is the "no connect /
/// payout account" case, which the consumer maps to whatever status code its boundary used before.</summary>
public interface IPayoutAccountClient
{
    Task<string?> GetOnboardingLinkAsync(Guid ownerId, CancellationToken ct = default);
    Task<PayoutAccountStatus> GetAccountStatusAsync(Guid ownerId, CancellationToken ct = default);
    Task<SavedCard?> GetPaymentMethodAsync(Guid ownerId, CancellationToken ct = default);
    Task<string?> CreateSetupIntentAsync(Guid ownerId, CancellationToken ct = default);
}
