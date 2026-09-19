using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Concertable.Payment.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payment");

            migrationBuilder.CreateTable(
                name: "CommissionConfigurations",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionConfigurations", x => x.Id);
                    table.CheckConstraint("CK_CommissionConfigurations_RatePercentage", "\"RatePercentage\" > 0 AND \"RatePercentage\" <= 100");
                });

            migrationBuilder.CreateTable(
                name: "FinancialOperations",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerAccounts",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTransactions",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostingType = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaymentIntentId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSessionOperations",
                schema: "payment",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Session = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayerOwnerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayeeOwnerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    FundsRouting = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentMethodId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderCustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderConnectedAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MandateTermsVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MandateAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FingerprintVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CurrentRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanceledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSessionOperations", x => x.OperationId);
                    table.CheckConstraint("CK_PaymentSessionOperations_CurrentRevision", "\"CurrentRevision\" >= 1");
                    table.CheckConstraint("CK_PaymentSessionOperations_FingerprintVersion", "\"FingerprintVersion\" >= 1");
                    table.CheckConstraint("CK_PaymentSessionOperations_MandateEvidence", "(\"MandateTermsVersion\" IS NULL AND \"MandateAcceptedAt\" IS NULL) OR (\"MandateTermsVersion\" IS NOT NULL AND \"MandateAcceptedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PaymentSessionOperations_RequestFingerprint", "char_length(\"RequestFingerprint\") = 64");
                });

            migrationBuilder.CreateTable(
                name: "PayoutAccounts",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    StripeAccountId = table.Column<string>(type: "text", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StripeEvents",
                schema: "payment",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "text", nullable: false),
                    EventProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "CommissionBindings",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayerReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BoundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StripeSetupIntentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewedGrossMinor = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionBindings_CommissionConfigurations_CommissionConfi~",
                        column: x => x.CommissionConfigurationId,
                        principalSchema: "payment",
                        principalTable: "CommissionConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LedgerTransactionId = table.Column<int>(type: "integer", nullable: false),
                    LedgerAccountId = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalSchema: "payment",
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalSchema: "payment",
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSessionAttempts",
                schema: "payment",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderObjectKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderObjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderDiagnosticCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderDiagnosticMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextReconcileAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminalAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CaptureBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentMethodId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastProviderEventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastProviderEventCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSessionAttempts", x => x.AttemptId);
                    table.CheckConstraint("CK_PaymentSessionAttempts_ProviderBinding", "\"ProviderObjectId\" IS NOT NULL OR \"State\" = 'Creating'");
                    table.CheckConstraint("CK_PaymentSessionAttempts_Revision", "\"Revision\" >= 1");
                    table.ForeignKey(
                        name: "FK_PaymentSessionAttempts_PaymentSessionOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "payment",
                        principalTable: "PaymentSessionOperations",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Escrows",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionBindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PayeeGrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    CommissionGrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    CommissionNetMinor = table.Column<long>(type: "bigint", nullable: false),
                    CommissionVatMinor = table.Column<long>(type: "bigint", nullable: false),
                    CommissionVatRatePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    PayerTotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChargeId = table.Column<string>(type: "text", nullable: false),
                    TransferId = table.Column<string>(type: "text", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseOperationFingerprintVersion = table.Column<int>(type: "integer", nullable: true),
                    ReleaseOperationFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    RefundedGrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escrows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Escrows_CommissionBindings_CommissionBindingId",
                        column: x => x.CommissionBindingId,
                        principalSchema: "payment",
                        principalTable: "CommissionBindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    Discriminator = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    CommissionBindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PayeeGrossMinor = table.Column<long>(type: "bigint", nullable: true),
                    CommissionGrossMinor = table.Column<long>(type: "bigint", nullable: true),
                    CommissionNetMinor = table.Column<long>(type: "bigint", nullable: true),
                    CommissionVatMinor = table.Column<long>(type: "bigint", nullable: true),
                    CommissionVatRatePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    PayerTotalMinor = table.Column<long>(type: "bigint", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationFingerprintVersion = table.Column<int>(type: "integer", nullable: true),
                    OperationFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    RequiresAction = table.Column<bool>(type: "boolean", nullable: true),
                    RefundedGrossMinor = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_CommissionBindings_CommissionBindingId",
                        column: x => x.CommissionBindingId,
                        principalSchema: "payment",
                        principalTable: "CommissionBindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRefunds",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscrowId = table.Column<int>(type: "integer", nullable: true),
                    SettlementTransactionId = table.Column<int>(type: "integer", nullable: true),
                    StripeRefundId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GrossRefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    CommissionRefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    CommissionVatReversedMinor = table.Column<long>(type: "bigint", nullable: false),
                    PayerTotalRefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRefunds", x => x.Id);
                    table.CheckConstraint("CK_PaymentRefunds_Owner", "(\"EscrowId\" IS NULL AND \"SettlementTransactionId\" IS NOT NULL) OR (\"EscrowId\" IS NOT NULL AND \"SettlementTransactionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentRefunds_Escrows_EscrowId",
                        column: x => x.EscrowId,
                        principalSchema: "payment",
                        principalTable: "Escrows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRefunds_Transactions_SettlementTransactionId",
                        column: x => x.SettlementTransactionId,
                        principalSchema: "payment",
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionBindings_CommissionConfigurationId",
                schema: "payment",
                table: "CommissionBindings",
                column: "CommissionConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionBindings_ExternalReference_PayerReference",
                schema: "payment",
                table: "CommissionBindings",
                columns: new[] { "ExternalReference", "PayerReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionBindings_StripePaymentIntentId",
                schema: "payment",
                table: "CommissionBindings",
                column: "StripePaymentIntentId",
                unique: true,
                filter: "\"StripePaymentIntentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionBindings_StripeSetupIntentId",
                schema: "payment",
                table: "CommissionBindings",
                column: "StripeSetupIntentId",
                unique: true,
                filter: "\"StripeSetupIntentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ChargeId",
                schema: "payment",
                table: "Escrows",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_CommissionBindingId",
                schema: "payment",
                table: "Escrows",
                column: "CommissionBindingId",
                unique: true,
                filter: "\"CommissionBindingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_OperationType_ClientReference",
                schema: "payment",
                table: "Escrows",
                columns: new[] { "OperationType", "ClientReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ReleaseOperationId",
                schema: "payment",
                table: "Escrows",
                column: "ReleaseOperationId",
                unique: true,
                filter: "\"ReleaseOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_Status",
                schema: "payment",
                table: "Escrows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_OperationType_ClientReference",
                schema: "payment",
                table: "FinancialOperations",
                columns: new[] { "OperationType", "ClientReference" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_Status",
                schema: "payment",
                table: "FinancialOperations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_Type_OwnerId_Currency",
                schema: "payment",
                table: "LedgerAccounts",
                columns: new[] { "Type", "OwnerId", "Currency" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerAccountId",
                schema: "payment",
                table: "LedgerEntries",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerTransactionId",
                schema: "payment",
                table: "LedgerEntries",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_OperationType_ClientReference",
                schema: "payment",
                table: "LedgerTransactions",
                columns: new[] { "OperationType", "ClientReference" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_PaymentIntentId",
                schema: "payment",
                table: "LedgerTransactions",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "UX_LedgerTransactions_PostingType_ExternalId",
                schema: "payment",
                table: "LedgerTransactions",
                columns: new[] { "PostingType", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRefunds_EscrowId",
                schema: "payment",
                table: "PaymentRefunds",
                column: "EscrowId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRefunds_OperationId",
                schema: "payment",
                table: "PaymentRefunds",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRefunds_SettlementTransactionId",
                schema: "payment",
                table: "PaymentRefunds",
                column: "SettlementTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRefunds_StripeRefundId",
                schema: "payment",
                table: "PaymentRefunds",
                column: "StripeRefundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessionAttempts_NextReconcileAt",
                schema: "payment",
                table: "PaymentSessionAttempts",
                column: "NextReconcileAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessionAttempts_State",
                schema: "payment",
                table: "PaymentSessionAttempts",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentSessionAttempts_OperationId_PredecessorAttemptId",
                schema: "payment",
                table: "PaymentSessionAttempts",
                columns: new[] { "OperationId", "PredecessorAttemptId" },
                unique: true,
                filter: "\"PredecessorAttemptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentSessionAttempts_OperationId_Revision",
                schema: "payment",
                table: "PaymentSessionAttempts",
                columns: new[] { "OperationId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentSessionAttempts_ProviderObjectKind_ProviderObjectId",
                schema: "payment",
                table: "PaymentSessionAttempts",
                columns: new[] { "ProviderObjectKind", "ProviderObjectId" },
                unique: true,
                filter: "\"ProviderObjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessionOperations_OperationType_ClientReference",
                schema: "payment",
                table: "PaymentSessionOperations",
                columns: new[] { "OperationType", "ClientReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessionOperations_PayeeOwnerKey",
                schema: "payment",
                table: "PaymentSessionOperations",
                column: "PayeeOwnerKey");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSessionOperations_PayerOwnerKey",
                schema: "payment",
                table: "PaymentSessionOperations",
                column: "PayerOwnerKey");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAccounts_OwnerId",
                schema: "payment",
                table: "PayoutAccounts",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAccounts_StripeAccountId",
                schema: "payment",
                table: "PayoutAccounts",
                column: "StripeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAccounts_StripeCustomerId",
                schema: "payment",
                table: "PayoutAccounts",
                column: "StripeCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CommissionBindingId",
                schema: "payment",
                table: "Transactions",
                column: "CommissionBindingId",
                unique: true,
                filter: "\"CommissionBindingId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OperationId",
                schema: "payment",
                table: "Transactions",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OperationType_ClientReference",
                schema: "payment",
                table: "Transactions",
                columns: new[] { "OperationType", "ClientReference" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PayeeId",
                schema: "payment",
                table: "Transactions",
                column: "PayeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PayerId",
                schema: "payment",
                table: "Transactions",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentIntentId",
                schema: "payment",
                table: "Transactions",
                column: "PaymentIntentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialOperations",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "LedgerEntries",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "PaymentRefunds",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "PaymentSessionAttempts",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "PayoutAccounts",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "StripeEvents",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "LedgerAccounts",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "LedgerTransactions",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "Escrows",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "Transactions",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "PaymentSessionOperations",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "CommissionBindings",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "CommissionConfigurations",
                schema: "payment");
        }
    }
}
