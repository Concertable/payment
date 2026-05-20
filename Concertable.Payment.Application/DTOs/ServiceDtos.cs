namespace Concertable.Payment.Application.DTOs;

internal record EscrowResponse(int EscrowId, string ChargeId, EscrowStatus Status, string? ClientSecret = null);

internal record PaymentResponse
{
    public bool RequiresAction { get; set; }
    public string? ClientSecret { get; set; }
    public string? TransactionId { get; set; }
}

internal record CheckoutSession(string ClientSecret, string CustomerSession, string CustomerId);

internal record TransferResponse(string TransferId);

internal record RefundResponse(string RefundId);

internal record EscrowDto(
    int Id,
    int BookingId,
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    EscrowStatus Status,
    string ChargeId,
    string? TransferId,
    string? RefundId,
    DateTime? ReleasedAt,
    DateTime? RefundedAt);
