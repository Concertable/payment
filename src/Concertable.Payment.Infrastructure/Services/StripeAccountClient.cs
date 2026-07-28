using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Kernel.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using static Concertable.Payment.Contracts.TransactionTypes;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripeAccountClient : IStripeAccountClient
{
    private readonly string baseUri;
    private readonly AccountService accountService;
    private readonly AccountLinkService accountLinkService;
    private readonly CustomerService customerService;
    private readonly PaymentMethodService paymentMethodService;
    private readonly SetupIntentService setupIntentService;
    private readonly PaymentIntentService paymentIntentService;
    private readonly Stripe.CustomerSessionService customerSessionService;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public StripeAccountClient(
        IOptions<StripeSettings> stripeSettings,
        IConfiguration configuration,
        AccountService accountService,
        AccountLinkService accountLinkService,
        CustomerService customerService,
        PaymentMethodService paymentMethodService,
        SetupIntentService setupIntentService,
        PaymentIntentService paymentIntentService,
        Stripe.CustomerSessionService customerSessionService,
        IPayoutAccountRepository payoutAccountRepository)
    {
        StripeConfiguration.ApiKey = stripeSettings.Value.SecretKey;
        this.baseUri = configuration["BaseUri:http"]!;
        this.accountService = accountService;
        this.accountLinkService = accountLinkService;
        this.customerService = customerService;
        this.paymentMethodService = paymentMethodService;
        this.setupIntentService = setupIntentService;
        this.paymentIntentService = paymentIntentService;
        this.customerSessionService = customerSessionService;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Address = new AddressOptions { Country = "GB" },
        }, cancellationToken: ct);
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct) ?? PayoutAccountEntity.Create(ownerId, email);
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
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct) ?? PayoutAccountEntity.Create(ownerId, email);
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

        if (account.Requirements?.CurrentlyDue?.Any() == true ||
            account.Requirements?.PendingVerification?.Any() == true)
            return PayoutAccountStatus.Pending;

        return PayoutAccountStatus.NotVerified;
    }

    public async Task<string> CreateSetupIntentAsync(string? stripeCustomerId)
    {
        var intent = await setupIntentService.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = stripeCustomerId,
            PaymentMethodTypes = ["card"],
            Usage = stripeCustomerId is null ? "on_session" : "off_session",
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
        if (card is null) return null;

        return new PaymentMethodDto(card.Brand, card.Last4, (int)card.ExpMonth, (int)card.ExpYear);
    }

    public async Task<CheckoutSession> CreatePaymentSessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = metadata.GetValueAs<long>(PaymentMetadataKeys.Amount),
            Currency = metadata.GetValue(PaymentMetadataKeys.Currency),
            Customer = stripeCustomerId,
            SetupFutureUsage = "off_session",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
        }, cancellationToken: ct);

        var customerSession = await CreateCustomerSessionAsync(stripeCustomerId, ct);
        return new CheckoutSession(intent.ClientSecret, customerSession, stripeCustomerId);
    }

    public async Task<CheckoutSession> CreateSetupSessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await setupIntentService.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = stripeCustomerId,
            AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
            Usage = "off_session",
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
        }, cancellationToken: ct);

        var customerSession = await CreateCustomerSessionAsync(stripeCustomerId, ct);
        return new CheckoutSession(intent.ClientSecret, customerSession, stripeCustomerId);
    }

    public async Task<CheckoutSession> CreateVerifySessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = 100,
            Currency = "gbp",
            Customer = stripeCustomerId,
            SetupFutureUsage = "off_session",
            CaptureMethod = "manual",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
        }, cancellationToken: ct);

        var customerSession = await CreateCustomerSessionAsync(stripeCustomerId, ct);
        return new CheckoutSession(intent.ClientSecret, customerSession, stripeCustomerId);
    }

    public async Task<CheckoutSession> CreateHoldSessionAsync(
        string stripeCustomerId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amount.ToMinorUnits(),
            Currency = "gbp",
            Customer = stripeCustomerId,
            SetupFutureUsage = "off_session",
            CaptureMethod = "manual",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value),
        }, cancellationToken: ct);

        var customerSession = await CreateCustomerSessionAsync(stripeCustomerId, ct);
        return new CheckoutSession(intent.ClientSecret, customerSession, stripeCustomerId);
    }

    private async Task<string> CreateCustomerSessionAsync(string stripeCustomerId, CancellationToken ct)
    {
        var session = await customerSessionService.CreateAsync(new CustomerSessionCreateOptions
        {
            Customer = stripeCustomerId,
            Components = new CustomerSessionComponentsOptions
            {
                PaymentElement = new CustomerSessionComponentsPaymentElementOptions
                {
                    Enabled = true,
                    Features = new CustomerSessionComponentsPaymentElementFeaturesOptions
                    {
                        PaymentMethodSave = "enabled",
                        PaymentMethodRemove = "enabled",
                        PaymentMethodRedisplay = "enabled",
                        PaymentMethodAllowRedisplayFilters = ["always", "limited", "unspecified"],
                    },
                },
            },
        }, cancellationToken: ct);
        return session.ClientSecret;
    }
}
