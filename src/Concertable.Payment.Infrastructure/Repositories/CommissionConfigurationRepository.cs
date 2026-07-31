using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionConfigurationRepository
    : GuidRepository<CommissionConfigurationEntity>, ICommissionConfigurationRepository
{
    public CommissionConfigurationRepository(PaymentDbContext context)
        : base(context) { }

    public override Task<CommissionConfigurationEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        context.CommissionConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, ct);
}
