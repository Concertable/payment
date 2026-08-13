using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class LedgerTransactionRepository : Repository<LedgerTransactionEntity>, ILedgerTransactionRepository
{
    public LedgerTransactionRepository(PaymentDbContext context)
        : base(context) { }
}
