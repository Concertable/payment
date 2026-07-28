using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class EscrowEntityConfiguration : IEntityTypeConfiguration<EscrowEntity>
{
    public void Configure(EntityTypeBuilder<EscrowEntity> builder)
    {
        builder.ToTable(Schema.Tables.Escrows, Schema.Name);
        builder.ComplexProperty(e => e.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount");
            money.Property(m => m.Currency).HasColumnName("Currency");
        });
        builder.ComplexProperty(e => e.PlatformFee, money =>
        {
            money.Property(m => m.Amount).HasColumnName("PlatformFee");
            money.Property(m => m.Currency).HasColumnName("PlatformFeeCurrency");
        });
        builder.HasIndex(e => e.BookingId).IsUnique();
        builder.HasIndex(e => e.ChargeId).IsUnique();
        builder.HasIndex(e => e.Status);
    }
}
