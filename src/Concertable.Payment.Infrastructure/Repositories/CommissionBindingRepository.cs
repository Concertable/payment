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
            .Include(a => a.CommissionConfiguration)
            .SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<CommissionBindingEntity?> GetByIdentityAsync(
        string externalReference,
        string payerReference,
        CancellationToken ct = default) =>
        context.CommissionBindings
            .Include(a => a.CommissionConfiguration)
            .SingleOrDefaultAsync(
                a => a.ExternalReference == externalReference &&
                     a.PayerReference == payerReference,
                ct);
}
