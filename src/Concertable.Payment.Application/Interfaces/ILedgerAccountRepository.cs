using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ILedgerAccountRepository : IRepository<LedgerAccountEntity>
{
    Task<LedgerAccountEntity?> FindAsync(LedgerAccountType type, Guid? ownerId, Currency currency, CancellationToken ct = default);
}
