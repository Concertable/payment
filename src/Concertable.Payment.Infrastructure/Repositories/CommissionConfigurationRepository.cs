using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionConfigurationRepository
    : GuidRepository<CommissionConfigurationEntity>, ICommissionConfigurationRepository
{
    public CommissionConfigurationRepository(PaymentDbContext context)
        : base(context) { }

    public Task<CommissionConfigurationEntity> GetOrCreateAsync(
        CommissionConfigurationEntity candidate,
        CancellationToken ct = default) =>
        context.CommissionConfigurations.GetOrCreateAsync(
            candidate,
            c => new { c.Id, c.Version },
            c => c.Id == candidate.Id || c.Version == candidate.Version,
            ct);
}
