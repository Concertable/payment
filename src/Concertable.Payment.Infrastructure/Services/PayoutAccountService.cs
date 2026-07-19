using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Services;

/// <summary>Owner-keyed payout-account logic shared by the HTTP <c>StripeAccountController</c> (Customer) and the
/// <c>PayoutAccount</c> gRPC service (B2B's proxy). The owner id is supplied by the caller — Payment stays
/// tenancy-agnostic and never reads it from a claim itself.</summary>
internal sealed class PayoutAccountService : IPayoutAccountService
{
    private readonly IPayoutAccountRepository repository;
    private readonly IStripeAccountClient stripeAccountClient;

    public PayoutAccountService(IPayoutAccountRepository repository, IStripeAccountClient stripeAccountClient)
    {
        this.repository = repository;
        this.stripeAccountClient = stripeAccountClient;
    }

    public async Task<string?> GetOnboardingLinkAsync(Guid ownerId, CancellationToken ct = default)
    {
        var account = await repository.GetByOwnerIdAsync(ownerId, ct);
        return account?.StripeAccountId is null ? null : await stripeAccountClient.GetOnboardingLinkAsync(account.StripeAccountId);
    }

    public async Task<PayoutAccountStatus> GetAccountStatusAsync(Guid ownerId, CancellationToken ct = default)
    {
        var account = await repository.GetByOwnerIdAsync(ownerId, ct);
        return account?.StripeAccountId is null ? PayoutAccountStatus.NotVerified : await stripeAccountClient.GetAccountStatusAsync(account.StripeAccountId);
    }

    public async Task<PaymentMethodDto?> GetPaymentMethodAsync(Guid ownerId, CancellationToken ct = default)
    {
        var account = await repository.GetByOwnerIdAsync(ownerId, ct);
        return account?.StripeCustomerId is null ? null : await stripeAccountClient.GetPaymentMethodDetailsAsync(account.StripeCustomerId);
    }

    public async Task<string?> CreateSetupIntentAsync(Guid ownerId, CancellationToken ct = default)
    {
        var account = await repository.GetByOwnerIdAsync(ownerId, ct);
        if (account is null) return null;

        var stripeCustomerId = account.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            await stripeAccountClient.ProvisionCustomerAsync(ownerId, account.Email, ct);
            account = await repository.GetByOwnerIdAsync(ownerId, ct);
            stripeCustomerId = account?.StripeCustomerId
                ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
        }

        return await stripeAccountClient.CreateSetupIntentAsync(stripeCustomerId);
    }
}
