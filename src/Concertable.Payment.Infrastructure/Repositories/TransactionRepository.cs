using Concertable.Contracts;
using Concertable.DataAccess.Infrastructure;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class TransactionRepository : Repository<TransactionEntity>, ITransactionRepository
{
    private readonly PaymentDbContext context;

    public TransactionRepository(PaymentDbContext context)
        : base(context)
    {
        this.context = context;
    }

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

    public Task<SettlementTransactionEntity?> GetSettlementByOperationIdAsync(
        Guid operationId,
        CancellationToken ct = default) =>
        context.SettlementTransactions.SingleOrDefaultAsync(
            transaction => transaction.OperationId == operationId,
            ct);

    public Task<SettlementTransactionEntity?> GetSettlementWithRefundsByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default) =>
        context.SettlementTransactions
            .Include(t => t.Refunds)
            .FirstOrDefaultAsync(t => t.BookingId == bookingId, ct);

    public async Task<long> GetCompletedTicketRevenueAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        await context.TicketTransactions
            .Where(t =>
                t.PayeeId == payeeId &&
                t.Status == TransactionStatus.Complete &&
                t.CreatedAt >= period.Start &&
                t.CreatedAt < period.End)
            .SumAsync(t => (long?)t.Amount, ct) ?? 0;

    public async Task<long> GetCompletedSettlementPayoutsAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default) =>
        await context.SettlementTransactions
            .Where(t =>
                t.PayeeId == payeeId &&
                t.Status == TransactionStatus.Complete &&
                t.CompletedAt >= period.Start &&
                t.CompletedAt < period.End)
            .SumAsync(t => (long?)t.PayeeGrossMinor, ct) ?? 0;

    public async Task<IReadOnlyList<MonthlyPaymentTotal>> GetCompletedTicketRevenueByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default)
    {
        var totals = await context.TicketTransactions
            .Where(t =>
                t.PayeeId == payeeId &&
                t.Status == TransactionStatus.Complete &&
                t.CreatedAt >= period.Start &&
                t.CreatedAt < period.End)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                GrossMinor = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ToListAsync(ct);

        return totals
            .Select(p => new MonthlyPaymentTotal(
                new DateOnly(p.Year, p.Month, 1),
                p.GrossMinor,
                p.GrossMinor,
                p.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyPaymentTotal>> GetCompletedSettlementPayoutsByMonthAsync(
        Guid payeeId,
        DateRange period,
        CancellationToken ct = default)
    {
        var totals = await context.SettlementTransactions
            .Where(t =>
                t.PayeeId == payeeId &&
                t.Status == TransactionStatus.Complete &&
                t.CompletedAt >= period.Start &&
                t.CompletedAt < period.End)
            .GroupBy(t => new { t.CompletedAt!.Value.Year, t.CompletedAt.Value.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                GrossMinor = g.Sum(t => t.PayeeGrossMinor),
                Count = g.Count()
            })
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ToListAsync(ct);

        return totals
            .Select(p => new MonthlyPaymentTotal(
                new DateOnly(p.Year, p.Month, 1),
                p.GrossMinor,
                p.GrossMinor,
                p.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<SettlementSummary>> GetRecentCompletedSettlementsAsync(
        Guid ownerId,
        int take,
        CancellationToken ct = default) =>
        await context.SettlementTransactions
            .Where(t =>
                (t.PayerId == ownerId || t.PayeeId == ownerId) &&
                t.Status == TransactionStatus.Complete &&
                t.CompletedAt.HasValue)
            .OrderByDescending(t => t.CompletedAt)
            .Take(take)
            .Select(t => new SettlementSummary(
                t.Id,
                t.BookingId,
                t.PayerId,
                t.PayeeId,
                t.PayeeGrossMinor,
                t.CompletedAt!.Value))
            .ToListAsync(ct);

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
