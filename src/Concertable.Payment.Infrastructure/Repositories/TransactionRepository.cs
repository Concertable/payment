using Concertable.Contracts;
using Concertable.DataAccess.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class TransactionRepository : Repository<TransactionEntity>, ITransactionRepository
{
    public TransactionRepository(PaymentDbContext context)
        : base(context) { }

    public Task<IPagination<TransactionEntity>> GetAsync(IPageParams pageParams, Guid userId)
    {
        var query = context.Transactions
            .Where(t => t.PayerId == userId || t.PayeeId == userId)
            .OrderByDescending(t => t.CreatedAt);

        return query.ToPaginationAsync(pageParams);
    }

    public Task<TransactionEntity?> GetByPaymentIntentIdAsync(string paymentIntentId) =>
        context.Transactions.FirstOrDefaultAsync(t => t.PaymentIntentId == paymentIntentId);

    public Task<SettlementTransactionEntity?> GetSettlementByCommissionAuthorizationIdAsync(
        Guid commissionAuthorizationId,
        CancellationToken ct = default) =>
        context.SettlementTransactions.SingleOrDefaultAsync(
            t => t.CommissionAuthorizationId == commissionAuthorizationId,
            ct);

    public Task<SettlementTransactionEntity?> GetSettlementWithRefundsByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default) =>
        context.SettlementTransactions
            .Include(t => t.Refunds)
            .FirstOrDefaultAsync(t => t.BookingId == bookingId, ct);

    public async Task CreateAsync(TransactionEntity entity)
    {
        await context.Transactions.AddAsync(entity);
        await context.SaveChangesAsync();
    }
}
