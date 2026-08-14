using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Client;

public sealed record ManagerSettlement(
    int Id,
    int BookingId,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    DateTime At);
