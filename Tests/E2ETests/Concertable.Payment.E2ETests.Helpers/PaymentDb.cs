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
}
