using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record CommissionCalculation(
    Guid CommissionConfigurationId,
    decimal RatePercentage,
    Money Gross,
    Money Commission,
    Money PayerTotal);
