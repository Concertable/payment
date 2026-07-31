using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.DTOs;

internal sealed record BoundCommission(
    CommissionBindingEntity Binding,
    CommissionConfigurationEntity Configuration,
    CommissionCalculation Calculation);
