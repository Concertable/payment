using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Concertable.Payment.Infrastructure.Data.Configurations;

internal sealed class PaymentSessionOperationEntityConfiguration
    : IEntityTypeConfiguration<PaymentSessionOperationEntity>
{
    public void Configure(EntityTypeBuilder<PaymentSessionOperationEntity> builder)
    {
        builder.ToTable(
            Schema.Tables.PaymentSessionOperations,
            Schema.Name,
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PaymentSessionOperations_CurrentRevision",
                    "[CurrentRevision] >= 1");
                table.HasCheckConstraint(
                    "CK_PaymentSessionOperations_FingerprintVersion",
                    "[FingerprintVersion] >= 1");
                table.HasCheckConstraint(
                    "CK_PaymentSessionOperations_RequestFingerprint",
                    "LEN([RequestFingerprint]) = 64");
            });
        builder.HasKey(operation => operation.OperationId);
        builder.Property(operation => operation.OperationId).ValueGeneratedNever();
        builder.Property(operation => operation.SessionKind).HasConversion<string>().HasMaxLength(40);
        builder.Property(operation => operation.Session).HasConversion<string>().HasMaxLength(20);
        builder.Property(operation => operation.OperationType).HasMaxLength(100);
        builder.Property(operation => operation.ConsumerCorrelation).HasMaxLength(200);
        builder.Property(operation => operation.PayerOwnerKey).HasMaxLength(200);
        builder.Property(operation => operation.PayeeOwnerKey).HasMaxLength(200);
        builder.Property(operation => operation.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(operation => operation.FundsRouting).HasConversion<string>().HasMaxLength(20);
        builder.Property(operation => operation.PaymentMethodId).HasMaxLength(100);
        builder.Property(operation => operation.ProviderCustomerId).HasMaxLength(100);
        builder.Property(operation => operation.ProviderConnectedAccountId).HasMaxLength(100);
        builder.Property(operation => operation.RequestFingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(operation => operation.RowVersion).IsRowVersion();
        builder.HasMany(operation => operation.Attempts)
            .WithOne()
            .HasForeignKey(attempt => attempt.OperationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(PaymentSessionOperationEntity.Attempts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(operation => operation.CurrentAttempt);
        builder.HasIndex(operation => new { operation.OperationType, operation.ConsumerCorrelation });
        builder.HasIndex(operation => operation.PayerOwnerKey);
        builder.HasIndex(operation => operation.PayeeOwnerKey);
    }
}

internal sealed class PaymentSessionAttemptEntityConfiguration
    : IEntityTypeConfiguration<PaymentSessionAttemptEntity>
{
    internal const string OperationRevisionIndex =
        "UX_PaymentSessionAttempts_OperationId_Revision";
    internal const string ProviderBindingIndex =
        "UX_PaymentSessionAttempts_ProviderObjectKind_ProviderObjectId";
    internal const string PredecessorIndex =
        "UX_PaymentSessionAttempts_OperationId_PredecessorAttemptId";

    public void Configure(EntityTypeBuilder<PaymentSessionAttemptEntity> builder)
    {
        builder.ToTable(
            Schema.Tables.PaymentSessionAttempts,
            Schema.Name,
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PaymentSessionAttempts_Revision",
                    "[Revision] >= 1");
                table.HasCheckConstraint(
                    "CK_PaymentSessionAttempts_ProviderBinding",
                    "[ProviderObjectId] IS NOT NULL OR [State] = 'Creating'");
            });
        builder.HasKey(attempt => attempt.AttemptId);
        builder.Property(attempt => attempt.AttemptId).ValueGeneratedNever();
        builder.Property(attempt => attempt.ProviderObjectKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(attempt => attempt.ProviderObjectId).HasMaxLength(100);
        builder.Property(attempt => attempt.State).HasConversion<string>().HasMaxLength(30);
        builder.Property(attempt => attempt.LastProviderStatus).HasMaxLength(100);
        builder.Property(attempt => attempt.FailureCode).HasConversion<string>().HasMaxLength(40);
        builder.Property(attempt => attempt.ProviderRequestId).HasMaxLength(100);
        builder.Property(attempt => attempt.ProviderDiagnosticCode).HasMaxLength(100);
        builder.Property(attempt => attempt.ProviderDiagnosticMessage).HasMaxLength(1000);
        builder.Property(attempt => attempt.LastProviderEventId).HasMaxLength(100);
        builder.Property(attempt => attempt.RowVersion).IsRowVersion();
        builder.HasIndex(attempt => new { attempt.OperationId, attempt.Revision })
            .IsUnique()
            .HasDatabaseName(OperationRevisionIndex);
        builder.HasIndex(attempt => new { attempt.ProviderObjectKind, attempt.ProviderObjectId })
            .IsUnique()
            .HasFilter("[ProviderObjectId] IS NOT NULL")
            .HasDatabaseName(ProviderBindingIndex);
        builder.HasIndex(attempt => new { attempt.OperationId, attempt.PredecessorAttemptId })
            .IsUnique()
            .HasFilter("[PredecessorAttemptId] IS NOT NULL")
            .HasDatabaseName(PredecessorIndex);
        builder.HasIndex(attempt => attempt.State);
        builder.HasIndex(attempt => attempt.NextReconcileAt);
    }
}
