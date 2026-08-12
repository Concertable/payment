using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class FinancialOperationEntityConfiguration : IEntityTypeConfiguration<FinancialOperationEntity>
{
    public void Configure(EntityTypeBuilder<FinancialOperationEntity> builder)
    {
        builder.ToTable(Schema.Tables.FinancialOperations, Schema.Name);
        builder.Property(operation => operation.Id).ValueGeneratedNever();
        builder.Property(operation => operation.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(operation => operation.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(operation => operation.RequestFingerprint).HasMaxLength(64);
        builder.Property(operation => operation.ReferenceId).HasMaxLength(100);
        builder.Property(operation => operation.FailureCode).HasMaxLength(200);
        builder.Property(operation => operation.FailureMessage).HasMaxLength(1000);
        builder.HasIndex(operation => new { operation.BookingId, operation.Type });
        builder.HasIndex(operation => operation.Status);
    }
}
