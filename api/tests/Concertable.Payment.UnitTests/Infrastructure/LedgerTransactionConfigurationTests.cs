using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class LedgerTransactionConfigurationTests
{
    [Fact]
    public void PostingIdentityIndex_IsUniqueAcrossTypeAndExternalId()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql("Host=localhost;Database=configuration-test;Username=postgres;Password=postgres")
            .Options;
        using var context = new PaymentDbContext(
            options,
            Options.Create(new OutboxOptions()),
            new PaymentConfigurationProvider());

        var index = context.Model
            .FindEntityType(typeof(LedgerTransactionEntity))!
            .GetIndexes()
            .Single(i => i.GetDatabaseName() == LedgerTransactionEntityConfiguration.PostingIdentityIndex);

        Assert.True(index.IsUnique);
        Assert.Equal(
            [nameof(LedgerTransactionEntity.PostingType), nameof(LedgerTransactionEntity.ExternalId)],
            index.Properties.Select(p => p.Name));
    }
}
