using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class PayoutAccountRepository : IPayoutAccountRepository
{
    private readonly PaymentDbContext context;

    public PayoutAccountRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<PayoutAccountEntity?> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default) =>
        context.PayoutAccounts.FirstOrDefaultAsync(a => a.OwnerId == ownerId, ct);

    public async Task AddAsync(PayoutAccountEntity entity, CancellationToken ct = default)
    {
        await context.PayoutAccounts.AddAsync(entity, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
