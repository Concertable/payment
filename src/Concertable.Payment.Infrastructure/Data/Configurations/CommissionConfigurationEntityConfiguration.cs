using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class CommissionConfigurationEntityConfiguration
    : IEntityTypeConfiguration<CommissionConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<CommissionConfigurationEntity> builder)
    {
        builder.ToTable(Schema.Tables.CommissionConfigurations, Schema.Name);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Version).HasMaxLength(100);
        builder.Property(c => c.Currency).HasConversion<string>().HasMaxLength(3);
        builder.HasIndex(c => c.Version).IsUnique();
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_CommissionConfigurations_RateBasisPoints",
                "[RateBasisPoints] >= 1 AND [RateBasisPoints] <= 10000");
            t.HasCheckConstraint(
                "CK_CommissionConfigurations_Currency",
                "[Currency] = 'Gbp'");
        });
    }
}
