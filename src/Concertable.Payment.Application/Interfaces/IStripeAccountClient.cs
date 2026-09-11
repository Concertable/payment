namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeAccountClient
{
    Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default);

    Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default);

    Task<string> GetOnboardingLinkAsync(string stripeAccountId);

    Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeAccountId);

    Task<string> CreateSetupIntentAsync(string? stripeCustomerId);

    Task<PaymentMethodDto?> GetPaymentMethodDetailsAsync(string stripeCustomerId);
}
