using Concertable.Payment.Application.DTOs;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeAccountClient : IStripeAccountClient
{
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public FakeStripeAccountClient(IPayoutAccountRepository payoutAccountRepository)
    {
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkCustomer($"cus_fake_{ownerId:N}");
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public async Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? PayoutAccountEntity.Create(ownerId, email);
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
        Task.FromResult<PaymentMethodDto?>(new("visa", "4242", 12, 2030));
}
