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
        builder.HasIndex(r => r.OperationId).IsUnique().HasFilter("[OperationId] IS NOT NULL");
        builder.HasOne(r => r.Escrow)
            .WithMany(e => e.Refunds)
            .HasForeignKey(r => r.EscrowId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.SettlementTransaction)
            .WithMany(t => t.Refunds)
            .HasForeignKey(r => r.SettlementTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
            t.HasCheckConstraint(
                "CK_PaymentRefunds_Owner",
                "([EscrowId] IS NULL AND [SettlementTransactionId] IS NOT NULL) OR " +
                "([EscrowId] IS NOT NULL AND [SettlementTransactionId] IS NULL)"));
    }
}
