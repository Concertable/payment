using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionAuthorizationClaimRepository
    : GuidRepository<CommissionAuthorizationClaimEntity>, ICommissionAuthorizationClaimRepository
{
    public CommissionAuthorizationClaimRepository(PaymentDbContext context)
        : base(context) { }

    public Task<CommissionAuthorizationClaimEntity?> GetByCommissionAuthorizationIdAsync(
        Guid commissionAuthorizationId,
        CancellationToken ct = default) =>
        context.CommissionAuthorizationClaims
            .SingleOrDefaultAsync(c => c.CommissionAuthorizationId == commissionAuthorizationId, ct);
}
