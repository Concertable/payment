using Concertable.DataAccess.Application;
using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Concertable.Payment.Infrastructure;

internal interface IUnitOfWork : IUnitOfWork<PaymentDbContext>;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext context;

    public UnitOfWork(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task<bool> TrySaveChangesAsync(
        Func<DbUpdateException, bool> isExpected,
        CancellationToken cancellationToken = default) =>
        context.TrySaveChangesAsync(isExpected, cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(cancellationToken);

    public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);

    public Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            var result = await operation();
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
}
