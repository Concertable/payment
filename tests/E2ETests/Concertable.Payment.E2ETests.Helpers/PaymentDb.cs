using System.Data;
using Concertable.Payment.Domain.Enums;
using Dapper;

namespace Concertable.E2ETests;

public sealed class PaymentDb
{
    private readonly IDbConnection connection;

    public PaymentDb(IDbConnection connection)
    {
        this.connection = connection;
    }

    public Task<string?> GetLatestSettlementPaymentIntentIdAsync(int bookingId) =>
        connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 PaymentIntentId
            FROM payment.Transactions
            WHERE Discriminator = 'SettlementTransactionEntity'
              AND ContextId = @bookingId
              AND PaymentIntentId LIKE 'pi[_]%'
            ORDER BY CreatedAt DESC
            """,
            new { bookingId });

    public Task<Guid?> GetEscrowPayeeIdAsync(int bookingId) =>
        connection.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT ToOwnerId FROM payment.Escrows WHERE BookingId = @bookingId",
            new { bookingId });

    public Task<string> GetEscrowPaymentIntentIdAsync(int bookingId) =>
        connection.QuerySingleAsync<string>(
            "SELECT ChargeId FROM payment.Escrows WHERE BookingId = @bookingId",
            new { bookingId });

    public Task<int?> GetEscrowStatusAsync(int bookingId) =>
        connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Status FROM payment.Escrows WHERE BookingId = @bookingId",
            new { bookingId });

    public Task<string?> GetEscrowRefundIdAsync(int bookingId) =>
        connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 r.StripeRefundId
            FROM payment.PaymentRefunds r
            JOIN payment.Escrows e ON e.Id = r.EscrowId
            WHERE e.BookingId = @bookingId
              AND r.StripeRefundId IS NOT NULL
            ORDER BY r.CompletedAt DESC
            """,
            new { bookingId });

    public Task<int> GetLedgerTransactionCountAsync(int bookingId) =>
        connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM payment.LedgerTransactions WHERE BookingId = @bookingId",
            new { bookingId });

    public Task<long> GetLedgerSignedSumAsync(int bookingId) =>
        connection.QuerySingleAsync<long>(
            """
            SELECT COALESCE(SUM(e.Amount), 0)
            FROM payment.LedgerEntries e
            JOIN payment.LedgerTransactions t ON t.Id = e.LedgerTransactionId
            WHERE t.BookingId = @bookingId
            """,
            new { bookingId });

    public Task<long> GetLedgerPlatformRevenueAsync(int bookingId) =>
        connection.QuerySingleAsync<long>(
            """
            SELECT COALESCE(-SUM(e.Amount), 0)
            FROM payment.LedgerEntries e
            JOIN payment.LedgerTransactions t ON t.Id = e.LedgerTransactionId
            JOIN payment.LedgerAccounts a ON a.Id = e.LedgerAccountId
            WHERE t.BookingId = @bookingId AND a.Type = @platformRevenue
            """,
            new { bookingId, platformRevenue = (int)LedgerAccountType.PlatformRevenue });
}
