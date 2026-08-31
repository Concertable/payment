using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class LedgerAccountRepository : Repository<LedgerAccountEntity>, ILedgerAccountRepository
{
    private readonly PaymentDbContext context;

    public LedgerAccountRepository(PaymentDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public Task<LedgerAccountEntity> GetOrCreateAsync(
        LedgerAccountType type,
        Guid? ownerId,
        Currency currency,
        CancellationToken ct = default)
    {
        var candidate = LedgerAccountEntity.Create(type, ownerId, currency);
        return context.LedgerAccounts.GetOrCreateAsync(
            candidate,
            a => new { a.Type, a.OwnerId, a.Currency },
            a => a.Type == type && a.OwnerId == ownerId && a.Currency == currency,
            ct);
    }
}
