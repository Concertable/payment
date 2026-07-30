namespace Concertable.Payment.Application.DTOs;

internal sealed record CheckoutSession(
    string ClientSecret,
    string CustomerSession,
    string CustomerId,
    string? StripeIntentId = null);

internal sealed record EscrowDto(
    int Id,
    int BookingId,
    Guid FromOwnerId,
    Guid ToOwnerId,
    decimal Amount,
    EscrowStatus Status,
    string ChargeId,
    string? TransferId,
    DateTime? ReleasedAt);
