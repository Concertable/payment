using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionAuthorizationRepository
    : GuidRepository<CommissionAuthorizationEntity>, ICommissionAuthorizationRepository
{
    public CommissionAuthorizationRepository(PaymentDbContext context)
        : base(context) { }

    public override Task<CommissionAuthorizationEntity?> GetByIdAsync(
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
}
