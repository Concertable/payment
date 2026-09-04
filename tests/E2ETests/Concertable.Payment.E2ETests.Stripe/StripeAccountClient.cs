using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Stripe;
using PayoutAccountStatus = Concertable.Payment.Domain.Enums.PayoutAccountStatus;

namespace Concertable.Payment.E2ETests.Stripe;

internal sealed class StripeAccountClient : IStripeAccountClient
{
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly StripeAccountResolver resolver;
    private readonly SetupIntentService setupIntentService;
    private readonly PaymentMethodService paymentMethodService;

    public StripeAccountClient(
        IConfiguration configuration,
        IPayoutAccountRepository payoutAccountRepository,
        StripeAccountResolver resolver,
        SetupIntentService setupIntentService,
        PaymentMethodService paymentMethodService)
    {
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        this.payoutAccountRepository = payoutAccountRepository;
        this.resolver = resolver;
        this.setupIntentService = setupIntentService;
        this.paymentMethodService = paymentMethodService;
    }

    public async Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        if (!resolver.ResolveCustomer(ownerId).TryGetValue(out var id))
            return;

        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkCustomer(id);
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public async Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        if (!resolver.ResolveAccount(ownerId).TryGetValue(out var id))
            return;

        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkAccount(id);
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public Task<string> GetOnboardingLinkAsync(string stripeAccountId) =>
        Task.FromResult("https://connect.stripe.com/e2e-onboarding");

    public Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeAccountId) =>
        Task.FromResult(PayoutAccountStatus.Verified);

    public async Task<string> CreateSetupIntentAsync(string? stripeCustomerId)
    {
        var intent = await setupIntentService.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = stripeCustomerId,
            PaymentMethodTypes = ["card"],
            Usage = stripeCustomerId is null ? "on_session" : "off_session"
        });
        return intent.ClientSecret;
    }

    public async Task<PaymentMethodDto?> GetPaymentMethodDetailsAsync(string stripeCustomerId)
    {
        var paymentMethods = await paymentMethodService.ListAsync(new PaymentMethodListOptions
        {
            Customer = stripeCustomerId,
            Type = "card"
        });
        var card = paymentMethods.FirstOrDefault()?.Card;
        return card is null
            ? null
            : new PaymentMethodDto(card.Brand, card.Last4, (int)card.ExpMonth, (int)card.ExpYear);
    }
}
