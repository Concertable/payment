using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.DTOs;

internal sealed record BoundCommission(
    CommissionBindingEntity Binding,
    CommissionTerms Terms,
    Concertable.Payment.Domain.CommissionCalculation Calculation);
