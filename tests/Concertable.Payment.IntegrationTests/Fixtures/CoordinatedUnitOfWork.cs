using Concertable.Payment.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Concertable.Payment.IntegrationTests.Fixtures;

internal sealed class CoordinatedUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork unitOfWork;
    private readonly TaskCompletionSource savesMayProceed;
    private readonly Func<int> incrementSaveCount;

    public CoordinatedUnitOfWork(
        IUnitOfWork unitOfWork,
        TaskCompletionSource savesMayProceed,
        Func<int> incrementSaveCount)
    {
        this.unitOfWork = unitOfWork;
        this.savesMayProceed = savesMayProceed;
        this.incrementSaveCount = incrementSaveCount;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (incrementSaveCount() == 2)
            savesMayProceed.TrySetResult();

        await savesMayProceed.Task.WaitAsync(cancellationToken);
        return await unitOfWork.TrySaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.BeginTransactionAsync(cancellationToken);

    public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(operation, cancellationToken);

    public Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(operation, cancellationToken);
}
