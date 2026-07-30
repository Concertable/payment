using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class CommissionAuthorizationEntityConfiguration
    : IEntityTypeConfiguration<CommissionAuthorizationEntity>
{
    public void Configure(EntityTypeBuilder<CommissionAuthorizationEntity> builder)
    {
        builder.ToTable(Schema.Tables.CommissionAuthorizations, Schema.Name);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ExternalReference).HasMaxLength(200);
        builder.Property(a => a.PayerReference).HasMaxLength(200);
        builder.Property(a => a.StripePaymentIntentId).HasMaxLength(100);
        builder.Property(a => a.StripeSetupIntentId).HasMaxLength(100);
        builder.HasIndex(a => new { a.ExternalReference, a.PayerReference }).IsUnique();
        builder.HasIndex(a => a.StripePaymentIntentId).IsUnique().HasFilter("[StripePaymentIntentId] IS NOT NULL");
        builder.HasIndex(a => a.StripeSetupIntentId).IsUnique().HasFilter("[StripeSetupIntentId] IS NOT NULL");
        builder.HasOne(a => a.CommissionConfiguration)
            .WithMany()
            .HasForeignKey(a => a.CommissionConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
