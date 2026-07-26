using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ILedgerTransactionRepository : IRepository<LedgerTransactionEntity>
{
    Task<bool> CommitPostingAsync(CancellationToken ct = default);
}
