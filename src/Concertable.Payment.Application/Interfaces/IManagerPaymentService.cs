using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IManagerPaymentService
{
    Task<Result<PaymentOutcome>> PayAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    /// <summary>
    /// The amount is known but cannot be charged yet — the other party hasn't accepted.
    /// Pre-authorises the card for a future off-session charge so the bank will honour it
    /// when the payer is no longer present.
    /// </summary>
    Task<CheckoutSession> CreateSetupSessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);

    /// <summary>
    /// The amount is unknown until after the event (e.g. door revenue split).
    /// Confirms the card is real and likely chargeable — nothing is ring-fenced.
    /// </summary>
    Task<CheckoutSession> CreateVerifySessionAsync(
        Guid payerId,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);

    /// <summary>
    /// The amount is known and ring-fenced now — the bank locks it, nothing is taken yet.
    /// A subsequent capture collects the held amount into escrow at accept time.
    /// </summary>
    Task<CheckoutSession> CreateHoldSessionAsync(
        Guid payerId,
        decimal amount,
        IDictionary<string, string> metadata,
        CancellationToken ct = default);

    Task<string> FindHeldIntentAsync(
        Guid payerId,
        int applicationId,
        CancellationToken ct = default);
}
