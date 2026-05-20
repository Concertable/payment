using Concertable.DataAccess.Infrastructure;
using Concertable.Payment.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data;

internal sealed class PaymentConfigurationProvider : IEntityTypeConfigurationProvider
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransactionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TicketTransactionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SettlementTransactionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VerifyTransactionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StripeEventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PayoutAccountEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EscrowEntityConfiguration());
    }
}
