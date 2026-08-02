using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record CommissionBinding(
    Guid BindingId,
    Guid CommissionConfigurationId,
    decimal RatePercentage,
    Currency Currency,
    CommissionQuote? Quote);
