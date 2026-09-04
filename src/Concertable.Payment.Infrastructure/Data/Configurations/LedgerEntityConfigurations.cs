using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class LedgerAccountEntityConfiguration : IEntityTypeConfiguration<LedgerAccountEntity>
{
    internal const string IdentityIndex = "IX_LedgerAccounts_Type_OwnerId_Currency";

    public void Configure(EntityTypeBuilder<LedgerAccountEntity> builder)
    {
        builder.ToTable(Schema.Tables.LedgerAccounts, Schema.Name);
        builder.HasIndex(a => new { a.Type, a.OwnerId, a.Currency })
            .IsUnique()
            .HasFilter(null)
            .HasDatabaseName(IdentityIndex);
    }
}

internal sealed class LedgerTransactionEntityConfiguration : IEntityTypeConfiguration<LedgerTransactionEntity>
{
    internal const string PostingIdentityIndex = "UX_LedgerTransactions_PostingType_ExternalId";

    public void Configure(EntityTypeBuilder<LedgerTransactionEntity> builder)
    {
        builder.ToTable(Schema.Tables.LedgerTransactions, Schema.Name);
        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(LedgerTransactionEntity.Entries))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(t => t.ExternalId).HasMaxLength(255);
        builder.HasIndex(t => new { t.PostingType, t.ExternalId })
            .IsUnique()
            .HasDatabaseName(PostingIdentityIndex);
        builder.Property(t => t.OperationType).HasMaxLength(200);
        builder.Property(t => t.ClientReference).HasMaxLength(200);
        builder.HasIndex(t => new { t.OperationType, t.ClientReference });
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
