using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record CommissionQuote(
    Guid CommissionConfigurationId,
    string ConfigurationVersion,
    int RateBasisPoints,
    Currency Currency,
    long GrossMinor,
    long CommissionMinor,
    long PayerTotalMinor);
