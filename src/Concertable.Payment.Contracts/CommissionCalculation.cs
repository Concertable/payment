using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record CommissionCalculation(
    Guid CommissionConfigurationId,
    decimal RatePercentage,
    Currency Currency,
    long GrossMinor,
    long CommissionMinor,
    long PayerTotalMinor);
