using Reunion;
using Concertable.Payment.Client.Enums;

namespace Concertable.Payment.Client;

public interface IPayoutAccountOperationsClient
{
    Task<Option<string>> GetOnboardingLinkAsync(Guid ownerId, CancellationToken ct = default);
    Task<PayoutAccountStatus> GetAccountStatusAsync(Guid ownerId, CancellationToken ct = default);
    Task<Option<SavedCard>> GetPaymentMethodAsync(Guid ownerId, CancellationToken ct = default);
    Task<Option<string>> CreateSetupIntentAsync(Guid ownerId, CancellationToken ct = default);
}
