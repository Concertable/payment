using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionBindingRepository
    : GuidRepository<CommissionBindingEntity>, ICommissionBindingRepository
{
    public CommissionBindingRepository(PaymentDbContext context)
        : base(context) { }

    public async Task<CommissionBindingEntity> GetOrCreateAsync(
        CommissionBindingEntity candidate,
        CancellationToken ct = default)
    {
        return await context.CommissionBindings.GetOrCreateAsync(
            candidate,
            a => new { a.ExternalReference, a.PayerReference },
            a => a.ExternalReference == candidate.ExternalReference &&
                 a.PayerReference == candidate.PayerReference,
            ct);
    }
}
