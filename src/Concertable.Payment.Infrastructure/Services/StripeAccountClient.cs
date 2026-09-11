using Concertable.Kernel.Exceptions;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripeAccountClient : IStripeAccountClient
{
    private readonly string baseUri;
    private readonly AccountService accountService;
    private readonly AccountLinkService accountLinkService;
    private readonly CustomerService customerService;
    private readonly PaymentMethodService paymentMethodService;
    private readonly SetupIntentService setupIntentService;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public StripeAccountClient(
        IOptions<StripeSettings> stripeSettings,
        IConfiguration configuration,
        AccountService accountService,
        AccountLinkService accountLinkService,
        CustomerService customerService,
        PaymentMethodService paymentMethodService,
        SetupIntentService setupIntentService,
        IPayoutAccountRepository payoutAccountRepository)
    {
        StripeConfiguration.ApiKey = stripeSettings.Value.SecretKey;
        this.baseUri = configuration["BaseUri:http"]!;
        this.accountService = accountService;
        this.accountLinkService = accountLinkService;
        this.customerService = customerService;
        this.paymentMethodService = paymentMethodService;
        this.setupIntentService = setupIntentService;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Address = new AddressOptions { Country = "GB" }
        }, cancellationToken: ct);
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkCustomer(customer.Id);
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public async Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var stripeAccount = await accountService.CreateAsync(new AccountCreateOptions
        {
            Type = "express",
            Email = email,
            Country = "GB",
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
            }
        }, cancellationToken: ct);
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct)
            ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkAccount(stripeAccount.Id);
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    public async Task<string> GetOnboardingLinkAsync(string stripeAccountId)
    {
        var link = await accountLinkService.CreateAsync(new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            RefreshUrl = $"{baseUri}/stripe-refresh",
            ReturnUrl = $"{baseUri}/stripe-return",
            Type = "account_onboarding"
        });
        return link.Url;
    }

    public async Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeAccountId)
    {
        var account = await accountService.GetAsync(stripeAccountId);
        if (account.ChargesEnabled && account.PayoutsEnabled)
            return PayoutAccountStatus.Verified;
        if (account.Requirements?.CurrentlyDue?.Any() == true
            || account.Requirements?.PendingVerification?.Any() == true)
            return PayoutAccountStatus.Pending;
        return PayoutAccountStatus.NotVerified;
    }

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
