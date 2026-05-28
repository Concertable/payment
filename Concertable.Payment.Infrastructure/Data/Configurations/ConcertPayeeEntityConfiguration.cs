using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal class ConcertPayeeEntityConfiguration : IEntityTypeConfiguration<ConcertPayeeEntity>
{
    public void Configure(EntityTypeBuilder<ConcertPayeeEntity> builder)
    {
        builder.ToTable("ConcertPayees", Schema.Name);
        builder.HasKey(x => x.ConcertId);
        builder.Property(x => x.ConcertId).ValueGeneratedNever();
    }
}
