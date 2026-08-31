using Concertable.Payment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Concertable.Payment.UnitTests.Infrastructure;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> TrySaveChangesAsync(
        Func<DbUpdateException, bool> isExpected,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> TrySaveChangesAsync(
        Func<DbUpdateException, bool> isExpected,
        CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();

    public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default) =>
        operation();
}
