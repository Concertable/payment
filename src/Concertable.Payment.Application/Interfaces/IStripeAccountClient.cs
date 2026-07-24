namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeAccountClient
{
    /// <summary>Creates a Stripe customer and links their ID to the owner's payout account row.</summary>
    Task ProvisionCustomerAsync(Guid ownerId, string email, CancellationToken ct = default);

    /// <summary>Creates a Stripe Express connect account and links it to the owner's payout account row.</summary>
    Task ProvisionConnectAccountAsync(Guid ownerId, string email, CancellationToken ct = default);

    /// <summary>Returns the Stripe-hosted onboarding URL for the given connect account.</summary>
    Task<string> GetOnboardingLinkAsync(string stripeAccountId);

    /// <summary>Returns whether the connect account has completed onboarding and has charges/payouts enabled.</summary>
    Task<PayoutAccountStatus> GetAccountStatusAsync(string stripeAccountId);

    /// <summary>
    /// Creates a <see cref="Stripe.SetupIntent"/> and returns its client secret. Used for the card-save
    /// onboarding flow (not the checkout form — for checkout use <see cref="CreateSetupSessionAsync"/>).
    /// </summary>
    Task<string> CreateSetupIntentAsync(string? stripeCustomerId);

    /// <summary>Returns the brand, last-four digits, and expiry of the customer's default saved card.</summary>
    Task<PaymentMethodDto?> GetPaymentMethodDetailsAsync(string stripeCustomerId);

    /// <summary>
    /// Creates a <see cref="Stripe.PaymentIntent"/> and a customer session, returning both client secrets.
    /// Used for on-session checkouts where the customer pays immediately (flat fee, door split, versus).
    /// </summary>
    Task<CheckoutSession> CreatePaymentSessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a <see cref="Stripe.SetupIntent"/> and a customer session, returning both client secrets.
    /// Used when the artist saves their card upfront (venue hire apply) so a hold can be placed off-session
    /// once the venue manager accepts.
    /// </summary>
    Task<CheckoutSession> CreateSetupSessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a manual-capture <see cref="Stripe.PaymentIntent"/> for £1 and a customer session.
    /// Used to verify the customer's card is valid before committing to a booking.
    /// </summary>
    Task<CheckoutSession> CreateVerifySessionAsync(
        string stripeCustomerId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a manual-capture <see cref="Stripe.PaymentIntent"/> for the hire fee and a customer session.
    /// Used when the venue manager accepts a venue hire application to place an authorisation hold on the
    /// artist's saved card; the hold is captured later when the concert completes.
    /// </summary>
    Task<CheckoutSession> CreateHoldSessionAsync(
        string stripeCustomerId,
        Money amount,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct = default);
}
