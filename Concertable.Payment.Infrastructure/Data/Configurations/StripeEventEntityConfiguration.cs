using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class StripeEventEntityConfiguration : IEntityTypeConfiguration<StripeEventEntity>
{
    public void Configure(EntityTypeBuilder<StripeEventEntity> builder)
    {
        builder.ToTable(Schema.Tables.StripeEvents, Schema.Name);
        builder.HasKey(e => e.EventId);
    }
}
