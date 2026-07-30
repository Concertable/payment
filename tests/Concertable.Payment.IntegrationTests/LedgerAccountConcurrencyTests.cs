using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class LedgerAccountConcurrencyTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public LedgerAccountConcurrencyTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task ConcurrentFirstPostings_ReconcileToSharedAccounts()
    {
        await using (var context = CreateContext())
            await context.Database.MigrateAsync();

        var payerId = Guid.NewGuid();
        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;

        async Task PostAsync(string paymentIntentId, int bookingId)
        {
            await using var context = CreateContext();
            var unitOfWork = new UnitOfWork(context);
            var ledger = new LedgerService(
                new LedgerAccountRepository(context),
                new LedgerTransactionRepository(context),
                TimeProvider.System);

            await ledger.StageAsync(
                LedgerPostings.EscrowHold(
                    payerId,
                    Money.Gbp(50),
                    bookingId,
                    paymentIntentId));

            if (Interlocked.Increment(ref readyCount) == 2)
                bothReady.SetResult();

            await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await unitOfWork.SaveChangesAsync();
        }

        await Task.WhenAll(
            PostAsync("pi_concurrent_1", 1),
            PostAsync("pi_concurrent_2", 2));

        await using var verificationContext = CreateContext();
        Assert.Equal(2, await verificationContext.LedgerAccounts.CountAsync());
        Assert.Equal(2, await verificationContext.LedgerTransactions.CountAsync());
        Assert.Equal(4, await verificationContext.LedgerEntries.CountAsync());
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;

        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
