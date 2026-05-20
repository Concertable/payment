using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal class PayoutAccountEntityConfiguration : IEntityTypeConfiguration<PayoutAccountEntity>
{
    public void Configure(EntityTypeBuilder<PayoutAccountEntity> builder)
    {
        builder.ToTable("PayoutAccounts", Schema.Name);
        builder.Property(a => a.Email).IsRequired();
        builder.HasIndex(a => a.UserId).IsUnique();
        builder.HasIndex(a => a.StripeAccountId);
        builder.HasIndex(a => a.StripeCustomerId);
    }
}
