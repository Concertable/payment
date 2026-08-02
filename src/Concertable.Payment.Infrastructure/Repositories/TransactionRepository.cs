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

    public Task<SettlementTransactionEntity?> GetSettlementByCommissionBindingIdAsync(
        Guid commissionBindingId,
        CancellationToken ct = default) =>
        context.SettlementTransactions.SingleOrDefaultAsync(
            t => t.CommissionBindingId == commissionBindingId,
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

    public async Task<bool> TryReserveSettlementRefundGrossAsync(
        int settlementId,
        long grossMinor,
        CancellationToken ct = default)
    {
        var affected = await context.SettlementTransactions
            .Where(t => t.Id == settlementId
                && t.Status == TransactionStatus.Complete
                && t.RefundedGrossMinor + grossMinor <= t.PayeeGrossMinor)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RefundedGrossMinor, t => t.RefundedGrossMinor + grossMinor),
                ct);
        return affected == 1;
    }

    public Task ReleaseReservedSettlementRefundGrossAsync(
        int settlementId,
        long grossMinor,
        CancellationToken ct = default) =>
        context.SettlementTransactions
            .Where(t => t.Id == settlementId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RefundedGrossMinor, t => t.RefundedGrossMinor - grossMinor),
                ct);
}
