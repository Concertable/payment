using Concertable.Payment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<TransactionEntity>
{
    public void Configure(EntityTypeBuilder<TransactionEntity> builder)
    {
        builder.ToTable(Schema.Tables.Transactions, Schema.Name);
        builder.Ignore(t => t.TransactionType);
        builder.HasIndex(t => t.PaymentIntentId).IsUnique();
        builder.HasIndex(t => t.PayerId);
        builder.HasIndex(t => t.PayeeId);
    }
}

internal sealed class TicketTransactionEntityConfiguration : IEntityTypeConfiguration<TicketTransactionEntity>
{
    public void Configure(EntityTypeBuilder<TicketTransactionEntity> builder)
    {
        builder.Property(t => t.ConcertId).HasColumnName("ContextId");
    }
}

internal sealed class SettlementTransactionEntityConfiguration : IEntityTypeConfiguration<SettlementTransactionEntity>
{
    public void Configure(EntityTypeBuilder<SettlementTransactionEntity> builder)
    {
        builder.Property(t => t.BookingId).HasColumnName("ContextId");
        builder.Property(t => t.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(t => t.CommissionVatRate)
            .HasConversion(rate => rate.Value, value => Percentage.From(value))
            .HasColumnName("CommissionVatRatePercentage")
            .HasPrecision(7, 4);
        builder.HasIndex(t => t.CommissionBindingId).IsUnique().HasFilter("[CommissionBindingId] IS NOT NULL");
        builder.Property(t => t.OperationFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(t => t.ClientSecret).HasMaxLength(500);
        builder.HasIndex(t => t.OperationId).IsUnique().HasFilter("[OperationId] IS NOT NULL");
        builder.HasOne(t => t.CommissionBinding)
            .WithMany()
            .HasForeignKey(t => t.CommissionBindingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VerifyTransactionEntityConfiguration : IEntityTypeConfiguration<VerifyTransactionEntity>
{
    public void Configure(EntityTypeBuilder<VerifyTransactionEntity> builder)
    {
        builder.Property(t => t.ApplicationId).HasColumnName("ContextId");
    }
}
