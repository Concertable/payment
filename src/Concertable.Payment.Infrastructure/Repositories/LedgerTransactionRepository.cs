using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class LedgerTransactionRepository(PaymentDbContext context)
    : Repository<LedgerTransactionEntity>(context), ILedgerTransactionRepository;
