using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal class PayoutAccountRepository : IPayoutAccountRepository
{
    private readonly PaymentDbContext context;

    public PayoutAccountRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<PayoutAccountEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        context.PayoutAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);

    public async Task AddAsync(PayoutAccountEntity entity, CancellationToken ct = default)
    {
        await context.PayoutAccounts.AddAsync(entity, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
