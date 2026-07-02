namespace Concertable.Payment.Application.DTOs;

internal sealed record EscrowDeposit(int EscrowId, string ChargeId, EscrowStatus Status, string? ClientSecret = null);

internal sealed record PaymentOutcome
{
    public bool RequiresAction { get; init; }
    public string? ClientSecret { get; init; }
    public string? TransactionId { get; init; }
}

internal sealed record CheckoutSession(string ClientSecret, string CustomerSession, string CustomerId);

internal sealed record Transfer(string TransferId);

internal sealed record Refund(string RefundId);

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
