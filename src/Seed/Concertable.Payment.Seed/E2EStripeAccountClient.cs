using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace Concertable.Payment.Seed;

/// <summary>
/// E2E substitute for <see cref="IStripeAccountClient"/> that intercepts account provisioning using
/// pre-seeded Stripe test-mode IDs from <see cref="StripeE2EAccountResolver"/>. Those IDs are fully
/// provisioned real Stripe test accounts — customers with saved test cards and connected Express accounts
/// with payouts enabled. Do NOT re-provision or recreate them; link them to payout account DB rows only.
/// <para>
/// Session-creation methods (<see cref="CreateSetupSessionAsync"/>, <see cref="CreatePaymentSessionAsync"/>,
/// <see cref="CreateHoldSessionAsync"/>, <see cref="CreateVerifySessionAsync"/>) call the real Stripe API
/// using those customer IDs, because <c>Stripe.js elements({ clientSecret })</c> validates client secrets
/// by calling <c>api.stripe.com/v1/elements/sessions</c> — only a real object passes that check.
/// Requires <c>ExternalServices:UseRealStripe = true</c> (guaranteed by <c>appsettings.E2E.json</c>) so
/// the Stripe SDK singletons are registered before <see cref="E2EServiceCollectionExtensions.UseE2EStripeClient"/>
/// swaps in this class.
/// </para>
/// </summary>
internal sealed class E2EStripeAccountClient : IStripeAccountClient
{
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly StripeE2EAccountResolver resolver;
    // CreateSetupSessionAsync (venue hire apply) + CreateSetupIntentAsync (onboarding card save)
    private readonly SetupIntentService setupIntentService;
    // CreatePaymentSessionAsync (flat fee / door split / versus), CreateVerifySessionAsync, CreateHoldSessionAsync (venue hire accept)
    private readonly PaymentIntentService paymentIntentService;
    // GetPaymentMethodDetailsAsync — reads the test card re-attached by @ResetsStripe before each scenario
    private readonly PaymentMethodService paymentMethodService;
    // Every session method needs a CustomerSession so Stripe Elements can render the saved-card UI
    private readonly Stripe.CustomerSessionService customerSessionService;

    public E2EStripeAccountClient(
        IConfiguration configuration,
        IPayoutAccountRepository payoutAccountRepository,
        StripeE2EAccountResolver resolver,
        SetupIntentService setupIntentService,
        PaymentIntentService paymentIntentService,
        PaymentMethodService paymentMethodService,
        Stripe.CustomerSessionService customerSessionService)
    {
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        this.payoutAccountRepository = payoutAccountRepository;
        this.resolver = resolver;
        this.setupIntentService = setupIntentService;
        this.paymentIntentService = paymentIntentService;
        this.paymentMethodService = paymentMethodService;
        this.customerSessionService = customerSessionService;
    }

    /// <summary>
    /// Links the pre-seeded Stripe customer ID from <see cref="StripeE2EAccountResolver"/> to the payout
    /// account DB row. Does not create a new Stripe customer — the customer already exists in test mode.
    /// </summary>
    public async Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        if (!resolver.TryGetCustomerId(ownerId, out var id))
            return;

        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct) ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkCustomer(id);
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Links the pre-seeded Stripe Express account ID from <see cref="StripeE2EAccountResolver"/> to the
    /// payout account DB row. Does not create a new Stripe account — the account already exists in test mode.
    /// </summary>
    public async Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default)
    {
        if (!resolver.TryGetAccountId(ownerId, out var id))
            return;

        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId, ct) ?? PayoutAccountEntity.Create(ownerId, email);
        account.LinkAccount(id);
        if (account.Id == 0)
            await payoutAccountRepository.AddAsync(account, ct);
        await payoutAccountRepository.SaveChangesAsync(ct);
    }

    /// <summary>Stubbed — the pre-seeded accounts are already onboarded in Stripe test mode.</summary>
    public Task<string> GetOnboardingLinkAsync(string stripeAccountId) =>
        Task.FromResult("https://connect.stripe.com/e2e-onboarding");

    /// <summary>Stubbed — the pre-seeded accounts are already verified in Stripe test mode.</summary>
    public Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeAccountId) =>
        Task.FromResult(PayoutAccountStatus.Verified);

    /// <summary>
    /// Creates a real Stripe <see cref="SetupIntent"/> so the client secret is valid when Stripe.js
    /// initialises the card-save onboarding flow.
    /// </summary>
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

    /// <summary>
    /// Reads the saved card from Stripe — the test card is re-attached to the customer by
    /// <c>@ResetsStripe</c> before each scenario, so a real lookup is needed here.
    /// </summary>
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

    /// <summary>
    /// Creates a real Stripe <see cref="PaymentIntent"/> against the pre-seeded customer.
    /// Real object required because <c>Stripe.js elements({ clientSecret })</c> validates against
    /// <c>api.stripe.com/v1/elements/sessions</c> and rejects fake secrets.
    /// </summary>
    public async Task<CheckoutSession> CreatePaymentSessionAsync(
        string stripeCustomerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = long.Parse(metadata["amount"]),
            Currency = metadata.TryGetValue("currency", out var c) ? c : "GBP",
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

    /// <summary>
    /// Creates a real Stripe <see cref="SetupIntent"/> against the pre-seeded customer so the artist's
    /// card can be saved for the off-session hold that fires when the venue manager accepts.
    /// Real object required because <c>Stripe.js elements({ clientSecret })</c> validates against
    /// <c>api.stripe.com/v1/elements/sessions</c> and rejects fake secrets.
    /// </summary>
    public async Task<CheckoutSession> CreateSetupSessionAsync(
        string stripeCustomerId,
        IDictionary<string, string> metadata,
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

    /// <summary>
    /// Creates a real Stripe <see cref="PaymentIntent"/> with <c>capture_method=manual</c> and amount £1
    /// to verify the customer's card is valid before committing to a booking.
    /// Real object required because <c>Stripe.js elements({ clientSecret })</c> validates against
    /// <c>api.stripe.com/v1/elements/sessions</c> and rejects fake secrets.
    /// </summary>
    public async Task<CheckoutSession> CreateVerifySessionAsync(
        string stripeCustomerId,
        IDictionary<string, string> metadata,
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

    /// <summary>
    /// Creates a real Stripe <see cref="PaymentIntent"/> with <c>capture_method=manual</c> to place an
    /// authorisation hold on the artist's saved card when the venue manager accepts the application.
    /// Real object required because <c>Stripe.js elements({ clientSecret })</c> validates against
    /// <c>api.stripe.com/v1/elements/sessions</c> and rejects fake secrets.
    /// </summary>
    public async Task<CheckoutSession> CreateHoldSessionAsync(
        string stripeCustomerId,
        decimal amount,
        IDictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var intent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100),
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
