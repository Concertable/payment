using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal class StripeEventEntityConfiguration : IEntityTypeConfiguration<StripeEventEntity>
{
    public void Configure(EntityTypeBuilder<StripeEventEntity> builder)
    {
        builder.ToTable("StripeEvents", Schema.Name);
        builder.HasKey(e => e.EventId);
    }
}
