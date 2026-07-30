using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.DTOs;

internal sealed record AuthorizedCommission(
    CommissionAuthorizationEntity Authorization,
    CommissionConfigurationEntity Configuration,
    CommissionCalculation Calculation);
