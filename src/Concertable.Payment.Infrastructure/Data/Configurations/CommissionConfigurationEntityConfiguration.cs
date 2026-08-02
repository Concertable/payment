using Concertable.Payment.Domain;
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
        builder.Property(c => c.Rate)
            .HasConversion(rate => rate.Value, value => Percentage.From(value))
            .HasColumnName("RatePercentage")
            .HasPrecision(7, 4);
        builder.ToTable(t =>
            t.HasCheckConstraint(
                "CK_CommissionConfigurations_RatePercentage",
                "[RatePercentage] > 0 AND [RatePercentage] <= 100"));
    }
}
