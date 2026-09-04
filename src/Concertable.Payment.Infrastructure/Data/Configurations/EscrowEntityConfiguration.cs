using Concertable.Payment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class EscrowEntityConfiguration : IEntityTypeConfiguration<EscrowEntity>
{
    public void Configure(EntityTypeBuilder<EscrowEntity> builder)
    {
        builder.ToTable(Schema.Tables.Escrows, Schema.Name);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(e => e.CommissionVatRate)
            .HasConversion(rate => rate.Value, value => Percentage.From(value))
            .HasColumnName("CommissionVatRatePercentage")
            .HasPrecision(7, 4);
        builder.Property(e => e.OperationType).HasMaxLength(200);
        builder.Property(e => e.ClientReference).HasMaxLength(200);
        builder.HasIndex(e => new { e.OperationType, e.ClientReference }).IsUnique();
        builder.HasIndex(e => e.ChargeId).IsUnique();
        builder.HasIndex(e => e.CommissionBindingId).IsUnique().HasFilter("[CommissionBindingId] IS NOT NULL");
        builder.Property(e => e.ReleaseOperationFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(e => e.ReleaseOperationId).IsUnique().HasFilter("[ReleaseOperationId] IS NOT NULL");
        builder.HasIndex(e => e.Status);
        builder.HasOne(e => e.CommissionBinding)
            .WithMany()
            .HasForeignKey(e => e.CommissionBindingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
