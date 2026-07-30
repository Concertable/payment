using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionConfigurationRepository : ICommissionConfigurationRepository
{
    private readonly PaymentDbContext context;

    public CommissionConfigurationRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<CommissionConfigurationEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        context.CommissionConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, ct);
}
