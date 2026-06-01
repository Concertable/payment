using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Data;

internal sealed class PaymentDbContext(
    DbContextOptions<PaymentDbContext> options,
    PaymentConfigurationProvider provider)
    : DbContextBase(options)
{
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<TicketTransactionEntity> TicketTransactions => Set<TicketTransactionEntity>();
    public DbSet<SettlementTransactionEntity> SettlementTransactions => Set<SettlementTransactionEntity>();
    public DbSet<StripeEventEntity> StripeEvents => Set<StripeEventEntity>();
    public DbSet<PayoutAccountEntity> PayoutAccounts => Set<PayoutAccountEntity>();
    public DbSet<EscrowEntity> Escrows => Set<EscrowEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema.Name);
        provider.Configure(modelBuilder);
    }
}
