using System.Data;
using Dapper;

namespace Concertable.E2ETests;

public sealed class PaymentDb
{
    private readonly IDbConnection connection;

    public PaymentDb(IDbConnection connection)
    {
        this.connection = connection;
    }

    public Task<PayoutAccountRow?> GetPayoutAccountByUserIdAsync(Guid userId) =>
        connection.QuerySingleOrDefaultAsync<PayoutAccountRow?>(
            "SELECT StripeAccountId, StripeCustomerId FROM payment.PayoutAccounts WHERE UserId = @userId",
            new { userId });

    public Task<string?> GetLatestSettlementPaymentIntentIdAsync(int bookingId) =>
        connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 t.PaymentIntentId
            FROM payment.SettlementTransactions st
            INNER JOIN payment.Transactions t ON t.Id = st.Id
            WHERE st.BookingId = @bookingId
              AND t.PaymentIntentId LIKE 'pi[_]%'
            ORDER BY t.CreatedAt DESC
            """,
            new { bookingId });
}

public sealed record PayoutAccountRow(string? StripeAccountId, string? StripeCustomerId);
