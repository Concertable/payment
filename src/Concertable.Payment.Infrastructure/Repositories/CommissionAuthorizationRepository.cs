using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionAuthorizationRepository : ICommissionAuthorizationRepository
{
    private readonly PaymentDbContext context;

    public CommissionAuthorizationRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<CommissionAuthorizationEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        context.CommissionAuthorizations
            .Include(a => a.CommissionConfiguration)
            .SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<CommissionAuthorizationEntity?> GetByIdentityAsync(
        string externalReference,
        string payerReference,
        CancellationToken ct = default) =>
        context.CommissionAuthorizations
            .Include(a => a.CommissionConfiguration)
            .SingleOrDefaultAsync(
                a => a.ExternalReference == externalReference &&
                     a.PayerReference == payerReference,
                ct);

    public Task AddAsync(
        CommissionAuthorizationEntity authorization,
        CancellationToken ct = default) =>
        context.CommissionAuthorizations.AddAsync(authorization, ct).AsTask();
}
