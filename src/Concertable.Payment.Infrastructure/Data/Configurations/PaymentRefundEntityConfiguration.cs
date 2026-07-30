using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class PaymentRefundEntityConfiguration : IEntityTypeConfiguration<PaymentRefundEntity>
{
    public void Configure(EntityTypeBuilder<PaymentRefundEntity> builder)
    {
        builder.ToTable(Schema.Tables.PaymentRefunds, Schema.Name);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.StripeRefundId).HasMaxLength(100);
        builder.HasIndex(r => r.StripeRefundId).IsUnique();
        builder.HasOne(r => r.Escrow)
            .WithMany(e => e.Refunds)
            .HasForeignKey(r => r.EscrowId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
