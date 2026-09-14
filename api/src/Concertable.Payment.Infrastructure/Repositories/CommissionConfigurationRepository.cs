using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionConfigurationRepository
    : GuidRepository<CommissionConfigurationEntity>, ICommissionConfigurationRepository
{
    private readonly PaymentDbContext context;

    public CommissionConfigurationRepository(PaymentDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public Task<CommissionConfigurationEntity> GetOrCreateAsync(
        CommissionConfigurationEntity candidate,
        CancellationToken ct = default) =>
        context.CommissionConfigurations.GetOrCreateAsync(
            candidate,
            c => c.Id,
            c => c.Id == candidate.Id,
            ct);
}
