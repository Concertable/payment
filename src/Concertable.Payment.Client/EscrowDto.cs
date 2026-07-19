using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;

namespace Concertable.Payment.Client;

public sealed record EscrowDto(
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
