namespace Concertable.Payment.Domain;

internal readonly record struct CommissionCalculation(
    Currency Currency,
    long PayeeGrossMinor,
    long CommissionGrossMinor,
    long CommissionNetMinor,
    long CommissionVatMinor,
    Percentage CommissionVatRate,
    long PayerTotalMinor);
