namespace Concertable.Payment.Application.DTOs;

internal sealed record CheckoutSession(string ClientSecret, string CustomerSession, string CustomerId);

internal sealed record EscrowDto(
    int Id,
    int BookingId,
    Guid FromOwnerId,
    Guid ToOwnerId,
    decimal Amount,
    EscrowStatus Status,
    string ChargeId,
    string? TransferId,
    string? RefundId,
    DateTime? ReleasedAt,
    DateTime? RefundedAt);
