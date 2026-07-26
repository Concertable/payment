using System;
using Microsoft.EntityFrameworkCore.Migrations;

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
                name: "Escrows",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    FromOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransferId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    PlatformFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformFeeCurrency = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escrows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerAccounts",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<int>(type: "int", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostingType = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutAccounts",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StripeAccountId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
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
                    EventId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EventProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Discriminator = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    ContextId = table.Column<int>(type: "int", nullable: true),
                    PlatformFee = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LedgerTransactionId = table.Column<int>(type: "int", nullable: false),
                    LedgerAccountId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_BookingId",
                schema: "payment",
                table: "Escrows",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ChargeId",
                schema: "payment",
                table: "Escrows",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_Status",
                schema: "payment",
                table: "Escrows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_Type_OwnerId_Currency",
                schema: "payment",
                table: "LedgerAccounts",
                columns: new[] { "Type", "OwnerId", "Currency" },
                unique: true);

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
                name: "IX_LedgerTransactions_BookingId",
                schema: "payment",
                table: "LedgerTransactions",
                column: "BookingId");

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
                name: "Escrows",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "LedgerEntries",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "PayoutAccounts",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "StripeEvents",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "Transactions",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "LedgerAccounts",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "LedgerTransactions",
                schema: "payment");
        }
    }
}
