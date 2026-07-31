using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record CommissionBinding(
    Guid BindingId,
    Guid CommissionConfigurationId,
    string ConfigurationVersion,
    int RateBasisPoints,
    Currency Currency,
    CommissionQuote? Quote);
