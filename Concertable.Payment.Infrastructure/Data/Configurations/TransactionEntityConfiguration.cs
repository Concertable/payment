using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal class TransactionEntityConfiguration : IEntityTypeConfiguration<TransactionEntity>
{
    public void Configure(EntityTypeBuilder<TransactionEntity> builder)
    {
        builder.ToTable("Transactions", Schema.Name);
        builder.Ignore(t => t.TransactionType);
        builder.HasIndex(t => t.PaymentIntentId).IsUnique();
        builder.HasIndex(t => t.FromUserId);
        builder.HasIndex(t => t.ToUserId);
    }
}

internal class TicketTransactionEntityConfiguration : IEntityTypeConfiguration<TicketTransactionEntity>
{
    public void Configure(EntityTypeBuilder<TicketTransactionEntity> builder)
    {
        builder.Property(t => t.ConcertId).HasColumnName("ContextId");
    }
}

internal class SettlementTransactionEntityConfiguration : IEntityTypeConfiguration<SettlementTransactionEntity>
{
    public void Configure(EntityTypeBuilder<SettlementTransactionEntity> builder)
    {
        builder.Property(t => t.BookingId).HasColumnName("ContextId");
    }
}

internal class VerifyTransactionEntityConfiguration : IEntityTypeConfiguration<VerifyTransactionEntity>
{
    public void Configure(EntityTypeBuilder<VerifyTransactionEntity> builder)
    {
        builder.Property(t => t.ApplicationId).HasColumnName("ContextId");
    }
}
