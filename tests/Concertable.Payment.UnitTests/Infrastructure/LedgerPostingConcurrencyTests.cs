using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class LedgerPostingConcurrencyTests
{
    [Fact]
    public async Task PostAsync_ConcurrentFirstUseOfAccount_CommitsBothPostingsAgainstOneAccount()
    {
        var databaseName = $"ledger-concurrency-{Guid.NewGuid():N}";
        var connectionString =
            $"Server=(localdb)\\mssqllocaldb;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var setupOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using (var setupContext = new PaymentDbContext(setupOptions, new PaymentConfigurationProvider()))
            await setupContext.Database.EnsureCreatedAsync();

        try
        {
            var saveBarrier = new ConcurrentSaveBarrier(2);
            var options = new DbContextOptionsBuilder<PaymentDbContext>()
                .UseSqlServer(connectionString)
                .AddInterceptors(saveBarrier)
                .Options;
            await using var firstContext = new PaymentDbContext(options, new PaymentConfigurationProvider());
            await using var secondContext = new PaymentDbContext(options, new PaymentConfigurationProvider());
            var firstLedger = CreateLedger(firstContext);
            var secondLedger = CreateLedger(secondContext);

            await Task.WhenAll(
                firstLedger.PostAsync(CreatePosting("first")),
                secondLedger.PostAsync(CreatePosting("second")));

            await using var verificationContext = new PaymentDbContext(setupOptions, new PaymentConfigurationProvider());
            var account = Assert.Single(await verificationContext.LedgerAccounts.ToListAsync());
            var transactions = await verificationContext.LedgerTransactions.CountAsync();
            var entries = await verificationContext.LedgerEntries.ToListAsync();

            Assert.Equal(LedgerAccountType.PlatformRevenue, account.Type);
            Assert.Equal(2, transactions);
            Assert.Equal(4, entries.Count);
            Assert.All(entries, entry => Assert.Equal(account.Id, entry.LedgerAccountId));
        }
        finally
        {
            await using var cleanupContext = new PaymentDbContext(setupOptions, new PaymentConfigurationProvider());
            await cleanupContext.Database.EnsureDeletedAsync();
        }
    }

    private static LedgerPostingService CreateLedger(PaymentDbContext context) =>
        new(
            new LedgerAccountRepository(context),
            new LedgerTransactionRepository(context),
            TimeProvider.System);

    private static LedgerPosting CreatePosting(string externalId) =>
        new(
            LedgerPostingType.DirectSettlement,
            externalId,
            1,
            externalId,
            [
                new PostingLeg(
                    new LedgerAccountRef(LedgerAccountType.PlatformRevenue, null),
                    LedgerDirection.Debit,
                    Money.Gbp(10)),
                new PostingLeg(
                    new LedgerAccountRef(LedgerAccountType.PlatformRevenue, null),
                    LedgerDirection.Credit,
                    Money.Gbp(10))
            ]);

    private sealed class ConcurrentSaveBarrier(int participants) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int remaining = participants;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref remaining) == 0)
                release.SetResult();

            if (Volatile.Read(ref remaining) >= 0)
                await release.Task.WaitAsync(cancellationToken);

            return result;
        }
    }
}
