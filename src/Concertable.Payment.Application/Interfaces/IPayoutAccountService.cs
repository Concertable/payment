using PayoutAccountStatus = Concertable.Payment.Application.Enums.PayoutAccountStatus;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPayoutAccountService
{
    Task<string?> GetOnboardingLinkAsync(Guid ownerId, CancellationToken ct = default);

    Task<PayoutAccountStatus> GetAccountStatusAsync(Guid ownerId, CancellationToken ct = default);

    Task<PaymentMethodDto?> GetPaymentMethodAsync(Guid ownerId, CancellationToken ct = default);

    Task<string?> CreateSetupIntentAsync(Guid ownerId, CancellationToken ct = default);
}
