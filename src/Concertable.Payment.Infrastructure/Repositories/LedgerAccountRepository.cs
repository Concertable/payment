using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class LedgerAccountRepository(PaymentDbContext context)
    : Repository<LedgerAccountEntity>(context), ILedgerAccountRepository
{
    public Task<LedgerAccountEntity?> FindAsync(LedgerAccountType type, Guid? ownerId, Currency currency, CancellationToken ct = default) =>
        context.LedgerAccounts.FirstOrDefaultAsync(a => a.Type == type && a.OwnerId == ownerId && a.Currency == currency, ct);
}
