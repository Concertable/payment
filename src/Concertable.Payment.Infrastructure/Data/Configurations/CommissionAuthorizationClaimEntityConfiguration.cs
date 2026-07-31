using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class CommissionAuthorizationClaimEntityConfiguration
    : IEntityTypeConfiguration<CommissionAuthorizationClaimEntity>
{
    public void Configure(EntityTypeBuilder<CommissionAuthorizationClaimEntity> builder)
    {
        builder.ToTable(Schema.Tables.CommissionAuthorizationClaims, Schema.Name);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Consumer).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => c.CommissionAuthorizationId).IsUnique();
        builder.HasOne(c => c.CommissionAuthorization)
            .WithMany()
            .HasForeignKey(c => c.CommissionAuthorizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
