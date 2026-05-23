using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Services;

internal class FakeStripeAccountClient : IStripeAccountClient
{
    private static readonly Dictionary<Guid, string> hardcodedCustomerIds = new()
    {
        [new Guid("c0000000-0000-0000-0000-000000000001")] = "cus_UIIy9Gbwfr3uAP",
        [new Guid("a1000000-0000-0000-0000-000000000001")] = "cus_UIIy5mCilBtJbR",
        [new Guid("a1000000-0000-0000-0000-000000000002")] = "cus_UIIy5415r69RmJ",
        [new Guid("b1000000-0000-0000-0000-000000000001")] = "cus_UIIymKfHijbNVO",
        [new Guid("b1000000-0000-0000-0000-000000000002")] = "cus_UIJ1qfgxYu624Q",
    };

    private static readonly Dictionary<Guid, string> hardcodedAccountIds = new()
    {
        [new Guid("a1000000-0000-0000-0000-000000000001")] = "acct_1TJiMePysoXmht10",
        [new Guid("a1000000-0000-0000-0000-000000000002")] = "acct_1TJiMoPupFslP2qz",
        [new Guid("b1000000-0000-0000-0000-000000000001")] = "acct_1TJiMjLxk4aCq1Ui",
        [new Guid("b1000000-0000-0000-0000-000000000002")] = "acct_1TJiPJLLwGSDilbV",
    };

    private static string ResolveCustomerId(Guid userId)
    {
        if (hardcodedCustomerIds.TryGetValue(userId, out var id)) return id;
        var s = userId.ToString("N");
        if (s.StartsWith("a1")) return $"cus_dev_artist_{int.Parse(s[20..])}";
        if (s.StartsWith("b1")) return $"cus_dev_venue_{int.Parse(s[20..])}";
        return $"cus_fake_{userId:N}";
    }

    private static string ResolveAccountId(Guid userId)
    {
        if (hardcodedAccountIds.TryGetValue(userId, out var id)) return id;
        var s = userId.ToString("N");
        if (s.StartsWith("a1")) return $"acct_dev_artist_{int.Parse(s[20..])}";
        if (s.StartsWith("b1")) return $"acct_dev_venue_{int.Parse(s[20..])}";
        return $"acct_fake_{userId:N}";
    }

    private readonly IPayoutAccountRepository payoutAccountRepository;

    public FakeStripeAccountClient(IPayoutAccountRepository payoutAccountRepository)
    {
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task ProvisionCustomerAsync(Guid userId, string email, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(userId, ct) ?? PayoutAccountEntity.Create(userId, email);
        account.LinkCustomer(ResolveCustomerId(userId));
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public async Task ProvisionConnectAccountAsync(Guid userId, string email, CancellationToken ct = default)
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(userId, ct) ?? PayoutAccountEntity.Create(userId, email);
        account.LinkAccount(ResolveAccountId(userId));
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public Task<string> GetOnboardingLinkAsync(string stripeId) =>
        Task.FromResult("https://fake-stripe-onboarding.local");

    public Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeId) =>
        Task.FromResult(PayoutAccountStatus.Verified);

    public Task<string> CreateSetupIntentAsync(string? stripeCustomerId) =>
        Task.FromResult("seti_fake_secret");

    public Task<PaymentMethodDto?> GetPaymentMethodDetailsAsync(string stripeCustomerId) =>
        Task.FromResult<PaymentMethodDto?>(new PaymentMethodDto("visa", "4242", 12, 2030));

    public Task<CheckoutSession> CreatePaymentSessionAsync(
        string stripeCustomerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("pi_fake_secret", "cuss_fake_secret", stripeCustomerId));

    public Task<CheckoutSession> CreateSetupSessionAsync(
        string stripeCustomerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("seti_fake_secret", "cuss_fake_secret", stripeCustomerId));

    public Task<CheckoutSession> CreateVerifySessionAsync(
        string stripeCustomerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("pi_fake_verify_secret", "cuss_fake_secret", stripeCustomerId));

    public Task<CheckoutSession> CreateHoldSessionAsync(
        string stripeCustomerId,
        decimal amount,
        IDictionary<string, string> metadata,
        CancellationToken ct = default) =>
        Task.FromResult(new CheckoutSession("pi_fake_hold_secret", "cuss_fake_secret", stripeCustomerId));
}
