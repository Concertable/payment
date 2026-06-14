using Concertable.Payment.Infrastructure.Data;
using Concertable.DataAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Concertable.Contracts;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class TransactionRepository : ITransactionRepository
{
    private readonly PaymentDbContext context;

    public TransactionRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<TransactionEntity?> GetByIdAsync(int id) =>
        context.Transactions.FirstOrDefaultAsync(t => t.Id == id);

    public bool Exists(int id) =>
        context.Transactions.Any(t => t.Id == id);

    public Task<IPagination<TransactionEntity>> GetAsync(IPageParams pageParams, Guid userId)
    {
        var query = context.Transactions
            .Where(t => t.PayerId == userId || t.PayeeId == userId)
            .OrderByDescending(t => t.CreatedAt);

        return query.ToPaginationAsync(pageParams);
    }

    public Task<TransactionEntity?> GetByPaymentIntentIdAsync(string paymentIntentId) =>
        context.Transactions.FirstOrDefaultAsync(t => t.PaymentIntentId == paymentIntentId);

    public async Task CreateAsync(TransactionEntity entity)
    {
        await context.Transactions.AddAsync(entity);
        await context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
