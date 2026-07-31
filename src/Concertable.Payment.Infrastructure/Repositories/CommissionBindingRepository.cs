using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionBindingRepository
    : GuidRepository<CommissionBindingEntity>, ICommissionBindingRepository
{
    public CommissionBindingRepository(PaymentDbContext context)
        : base(context) { }

    public override Task<CommissionBindingEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        context.CommissionBindings
            .SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<CommissionBindingEntity> GetOrCreateAsync(
        CommissionBindingEntity candidate,
        CancellationToken ct = default) =>
        context.CommissionBindings.GetOrCreateAsync(
            candidate,
            a => new { a.ExternalReference, a.PayerReference },
            a => a.ExternalReference == candidate.ExternalReference &&
                 a.PayerReference == candidate.PayerReference,
            ct);
}
