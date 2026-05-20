using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal class EscrowEntityConfiguration : IEntityTypeConfiguration<EscrowEntity>
{
    public void Configure(EntityTypeBuilder<EscrowEntity> builder)
    {
        builder.ToTable("Escrows", Schema.Name);
        builder.HasIndex(e => e.BookingId).IsUnique();
        builder.HasIndex(e => e.ChargeId).IsUnique();
        builder.HasIndex(e => e.Status);
    }
}
