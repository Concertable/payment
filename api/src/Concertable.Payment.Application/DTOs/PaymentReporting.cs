namespace Concertable.Payment.Application.DTOs;

internal sealed record MonthlyPaymentTotal(
    DateOnly Month,
    long GrossMinor,
    long NetMinor,
    int Count);

internal sealed record SettlementSummary(
    int Id,
    PaymentOperationReference Reference,
    Guid PayerId,
    Guid PayeeId,
    long AmountMinor,
    DateTime At);
