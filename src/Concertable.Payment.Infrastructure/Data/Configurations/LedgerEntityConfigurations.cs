using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class LedgerAccountEntityConfiguration : IEntityTypeConfiguration<LedgerAccountEntity>
{
    public void Configure(EntityTypeBuilder<LedgerAccountEntity> builder)
    {
        builder.ToTable(Schema.Tables.LedgerAccounts, Schema.Name);
        builder.HasIndex(a => new { a.Type, a.OwnerId, a.Currency }).IsUnique().HasFilter(null);
    }
}

internal sealed class LedgerTransactionEntityConfiguration : IEntityTypeConfiguration<LedgerTransactionEntity>
{
    public void Configure(EntityTypeBuilder<LedgerTransactionEntity> builder)
    {
        builder.ToTable(Schema.Tables.LedgerTransactions, Schema.Name);
        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(LedgerTransactionEntity.Entries))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(t => t.BookingId);
        builder.HasIndex(t => t.PaymentIntentId);
    }
}

internal sealed class LedgerEntryEntityConfiguration : IEntityTypeConfiguration<LedgerEntryEntity>
{
    public void Configure(EntityTypeBuilder<LedgerEntryEntity> builder)
    {
        builder.ToTable(Schema.Tables.LedgerEntries, Schema.Name);
        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.LedgerAccountId);
    }
}
