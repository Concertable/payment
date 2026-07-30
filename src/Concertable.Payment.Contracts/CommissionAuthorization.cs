using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record CommissionAuthorization(
    Guid AuthorizationId,
    Guid CommissionConfigurationId,
    string ConfigurationVersion,
    int RateBasisPoints,
    Currency Currency,
    CommissionQuote? Quote);
