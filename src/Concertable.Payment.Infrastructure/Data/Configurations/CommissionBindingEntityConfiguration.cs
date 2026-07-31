using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class CommissionBindingEntityConfiguration
    : IEntityTypeConfiguration<CommissionBindingEntity>
{
    public void Configure(EntityTypeBuilder<CommissionBindingEntity> builder)
    {
        builder.ToTable(Schema.Tables.CommissionBindings, Schema.Name);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Version).HasMaxLength(100);
        builder.Property(a => a.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(a => a.ExternalReference).HasMaxLength(200);
        builder.Property(a => a.PayerReference).HasMaxLength(200);
        builder.Property(a => a.StripePaymentIntentId).HasMaxLength(100);
        builder.Property(a => a.StripeSetupIntentId).HasMaxLength(100);
        builder.HasIndex(a => new { a.ExternalReference, a.PayerReference }).IsUnique();
        builder.HasIndex(a => a.StripePaymentIntentId).IsUnique().HasFilter("[StripePaymentIntentId] IS NOT NULL");
        builder.HasIndex(a => a.StripeSetupIntentId).IsUnique().HasFilter("[StripeSetupIntentId] IS NOT NULL");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_CommissionBindings_RateBasisPoints",
                "[RateBasisPoints] >= 1 AND [RateBasisPoints] <= 10000");
            t.HasCheckConstraint(
                "CK_CommissionBindings_VatRateBasisPoints",
                "[VatRateBasisPoints] >= 0 AND [VatRateBasisPoints] <= 10000");
            t.HasCheckConstraint(
                "CK_CommissionBindings_Currency",
                "[Currency] = 'Gbp'");
        });
    }
}
