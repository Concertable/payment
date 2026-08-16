using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class PayoutAccountRepository : Repository<PayoutAccountEntity>, IPayoutAccountRepository
{
    public PayoutAccountRepository(PaymentDbContext context) : base(context) { }

    public Task<PayoutAccountEntity?> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default) =>
        context.PayoutAccounts.FirstOrDefaultAsync(a => a.OwnerId == ownerId, ct);
}
