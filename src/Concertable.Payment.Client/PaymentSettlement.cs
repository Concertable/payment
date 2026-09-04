using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;

namespace Concertable.Payment.Client;

public sealed record PaymentSettlement(
    int Id,
    PaymentOperationReference Reference,
    Guid PayerId,
    Guid PayeeId,
    Money Amount,
    DateTime At);
