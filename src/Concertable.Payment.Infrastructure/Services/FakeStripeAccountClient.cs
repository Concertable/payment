using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Services;

/// <summary>
/// Dev-mode stub used when UseRealStripe=false. Skips all real Stripe API calls so you can
/// exercise business logic (checkout flows, escrow, etc.) without a live Stripe account.
/// Never used in E2E — E2EStripeAccountClient handles that.
/// </summary>
internal sealed class FakeStripeAccountClient : IStripeAccountClient
{
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public FakeStripeAccountClient(IPayoutAccountRepository payoutAccountRepository)
    {
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct) ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkCustomer($"cus_fake_{ownerId:N}");
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public async Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct) ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkAccount($"acct_fake_{ownerId:N}");
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public Task<string> GetOnboardingLinkAsync(string stripeAccountId) =>
        Task.FromResult("https://fake-stripe-onboarding.local");

    public Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeAccountId) =>
        Task.FromResult(PayoutAccountStatus.Verified);

    public Task<string> CreateSetupIntentAsync(string? stripeCustomerId) =>
        Task.FromResult("seti_fake_secret");

    public Task<PaymentMethodDto?> GetPaymentMethodDetailsAsync(string stripeCustomerId) =>
        Task.FromResult<PaymentMethodDto?>(new PaymentMethodDto("visa", "4242", 12, 2030));

    public Task<CheckoutSession> CreatePaymentSessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("pi_fake_secret", "cuss_fake_secret", stripeCustomerId));

    public Task<CheckoutSession> CreateSetupSessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("seti_fake_secret", "cuss_fake_secret", stripeCustomerId));

    public Task<CheckoutSession> CreateVerifySessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("pi_fake_verify_secret", "cuss_fake_secret", stripeCustomerId));

    public Task<CheckoutSession> CreateHoldSessionAsync(
        string stripeCustomerId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("pi_fake_hold_secret", "cuss_fake_secret", stripeCustomerId));
}
