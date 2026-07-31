using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionAuthorizationClaimRepository
    : IRepository<CommissionAuthorizationClaimEntity, Guid>
{
    Task<CommissionAuthorizationClaimEntity?> GetByCommissionAuthorizationIdAsync(
        Guid commissionAuthorizationId,
        CancellationToken ct = default);
}
