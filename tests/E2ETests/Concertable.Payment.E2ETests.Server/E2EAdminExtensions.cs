using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;

namespace Concertable.Payment.E2ETests.Server;

public static class E2EAdminExtensions
{
    private const string AdminKeyHeader = "X-Concertable-E2E-Key";
    private const int PlatformRevenueAccountType = 0;

    extension(IServiceCollection services)
    {
        public IServiceCollection AddPaymentE2EAdmin(IConfiguration configuration)
        {
            services.AddSingleton(new E2EAdminOptions(
                configuration["E2E:AdminKey"]
                    ?? throw new InvalidOperationException("E2E:AdminKey is required by the Payment E2E host."),
                configuration.GetConnectionString("PaymentDb")
                    ?? throw new InvalidOperationException("Connection string 'PaymentDb' is required by the Payment E2E host.")));
            return services;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication MapPaymentE2EAdmin()
        {
            var group = app.MapGroup("/_e2e")
                .AddEndpointFilter(AuthorizeAsync);
            group.MapPost("/reset", ResetAsync);
            group.MapGet("/bookings/{bookingId:int}/settlement-payment-intent-id", GetLatestSettlementPaymentIntentIdAsync);
            group.MapGet("/bookings/{bookingId:int}/escrow-payee-id", GetEscrowPayeeIdAsync);
            group.MapGet("/bookings/{bookingId:int}/escrow-payment-intent-id", GetEscrowPaymentIntentIdAsync);
            group.MapGet("/bookings/{bookingId:int}/escrow-status", GetEscrowStatusAsync);
            group.MapGet("/bookings/{bookingId:int}/escrow-refund-id", GetEscrowRefundIdAsync);
            group.MapGet("/bookings/{bookingId:int}/ledger-transaction-count", GetLedgerTransactionCountAsync);
            group.MapGet("/bookings/{bookingId:int}/ledger-signed-sum", GetLedgerSignedSumAsync);
            group.MapGet("/bookings/{bookingId:int}/ledger-platform-revenue", GetLedgerPlatformRevenueAsync);
            return app;
        }
    }

    private static async ValueTask<object?> AuthorizeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<E2EAdminOptions>();
        var supplied = context.HttpContext.Request.Headers[AdminKeyHeader].ToString();
        var expectedBytes = Encoding.UTF8.GetBytes(options.AdminKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        if (expectedBytes.Length != suppliedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
        {
            return Results.NotFound();
        }

        return await next(context);
    }

    private static async Task<IResult> ResetAsync(
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToIgnore = ["__EFMigrationsHistory", new Table("payment", "PayoutAccounts")],
            DbAdapter = DbAdapter.SqlServer,
            WithReseed = true,
        });
        await respawner.ResetAsync(connection);
        return Results.NoContent();
    }

    private static async Task<IResult> GetLatestSettlementPaymentIntentIdAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 PaymentIntentId
            FROM payment.Transactions
            WHERE Discriminator = 'SettlementTransactionEntity'
              AND ContextId = @bookingId
              AND PaymentIntentId LIKE 'pi[_]%'
            ORDER BY CreatedAt DESC
            """,
            new { bookingId });
        return Optional(value);
    }

    private static async Task<IResult> GetEscrowPayeeIdAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT ToOwnerId FROM payment.Escrows WHERE BookingId = @bookingId",
            new { bookingId });
        return Optional(value);
    }

    private static async Task<IResult> GetEscrowPaymentIntentIdAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<string>(
            "SELECT ChargeId FROM payment.Escrows WHERE BookingId = @bookingId",
            new { bookingId }));
    }

    private static async Task<IResult> GetEscrowStatusAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Status FROM payment.Escrows WHERE BookingId = @bookingId",
            new { bookingId });
        return Optional(value);
    }

    private static async Task<IResult> GetEscrowRefundIdAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 r.StripeRefundId
            FROM payment.PaymentRefunds r
            JOIN payment.Escrows e ON e.Id = r.EscrowId
            WHERE e.BookingId = @bookingId
              AND r.StripeRefundId IS NOT NULL
            ORDER BY r.CompletedAt DESC
            """,
            new { bookingId });
        return Optional(value);
    }

    private static async Task<IResult> GetLedgerTransactionCountAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM payment.LedgerTransactions WHERE BookingId = @bookingId",
            new { bookingId }));
    }

    private static async Task<IResult> GetLedgerSignedSumAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<long>(
            """
            SELECT COALESCE(SUM(e.Amount), 0)
            FROM payment.LedgerEntries e
            JOIN payment.LedgerTransactions t ON t.Id = e.LedgerTransactionId
            WHERE t.BookingId = @bookingId
            """,
            new { bookingId }));
    }

    private static async Task<IResult> GetLedgerPlatformRevenueAsync(
        int bookingId,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<long>(
            """
            SELECT COALESCE(-SUM(e.Amount), 0)
            FROM payment.LedgerEntries e
            JOIN payment.LedgerTransactions t ON t.Id = e.LedgerTransactionId
            JOIN payment.LedgerAccounts a ON a.Id = e.LedgerAccountId
            WHERE t.BookingId = @bookingId AND a.Type = @platformRevenue
            """,
            new { bookingId, platformRevenue = PlatformRevenueAccountType }));
    }

    private static IResult Optional(object? value) => value is null ? Results.NoContent() : Results.Ok(value);

    private static async Task<SqlConnection> OpenConnectionAsync(
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

internal sealed record E2EAdminOptions(string AdminKey, string ConnectionString);
