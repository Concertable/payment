using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class LedgerTransactionConfigurationTests
{
    [Fact]
    public void PostingIdentityIndex_IsUniqueAcrossTypeAndExternalId()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=localhost;Database=configuration-test;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PaymentDbContext(options, new PaymentConfigurationProvider());

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
