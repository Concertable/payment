using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class EscrowEntityConfiguration : IEntityTypeConfiguration<EscrowEntity>
{
    public void Configure(EntityTypeBuilder<EscrowEntity> builder)
    {
        builder.ToTable(Schema.Tables.Escrows, Schema.Name);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(3);
        builder.HasIndex(e => e.BookingId).IsUnique();
        builder.HasIndex(e => e.ChargeId).IsUnique();
        builder.HasIndex(e => e.CommissionAuthorizationId).IsUnique().HasFilter("[CommissionAuthorizationId] IS NOT NULL");
        builder.HasIndex(e => e.Status);
        builder.HasOne(e => e.CommissionAuthorization)
            .WithMany()
            .HasForeignKey(e => e.CommissionAuthorizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
