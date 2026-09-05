using System.Security.Cryptography;
using System.Text;
using Concertable.Kernel;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Respawn;
using Respawn.Graph;

namespace Concertable.Payment.E2ETests.Server;

public static class E2EAdminExtensions
{
    private const int PlatformRevenueAccountType = 0;

    extension(IServiceCollection services)
    {
        public IServiceCollection AddPaymentE2EAdmin(
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            E2EAdminSecurity.RequireE2EEnvironment(environment);
            var adminKey = configuration["E2E:AdminKey"];
            if (string.IsNullOrWhiteSpace(adminKey))
                throw new InvalidOperationException("E2E:AdminKey is required by the Payment E2E host.");

            services.AddSingleton(new E2EAdminOptions(
                adminKey,
                configuration.GetConnectionString("PaymentDb")
                    ?? throw new InvalidOperationException("Connection string 'PaymentDb' is required by the Payment E2E host.")));
            return services;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication MapPaymentE2EAdmin()
        {
            E2EAdminSecurity.RequireE2EEnvironment(app.Environment);
            var group = app.MapGroup("/_e2e")
                .AddEndpointFilter(AuthorizeAsync);
            group.MapPost("/reset", ResetAsync);
            group.MapGet("/operations/settlement-payment-intent-id", GetLatestSettlementPaymentIntentIdAsync);
            group.MapGet("/operations/escrow-payee-id", GetEscrowPayeeIdAsync);
            group.MapGet("/operations/escrow-payment-intent-id", GetEscrowPaymentIntentIdAsync);
            group.MapGet("/operations/escrow-status", GetEscrowStatusAsync);
            group.MapGet("/operations/escrow-refund-id", GetEscrowRefundIdAsync);
            group.MapGet("/operations/ledger-transaction-count", GetLedgerTransactionCountAsync);
            group.MapGet("/operations/ledger-signed-sum", GetLedgerSignedSumAsync);
            group.MapGet("/operations/ledger-platform-revenue", GetLedgerPlatformRevenueAsync);
            return app;
        }
    }

    private static async ValueTask<object?> AuthorizeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<E2EAdminOptions>();
        if (!E2EAdminSecurity.IsAuthorized(options.AdminKey, context.HttpContext.Request.Headers))
            return Results.NotFound();

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
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 PaymentIntentId
            FROM payment.Transactions
            WHERE Discriminator = 'SettlementTransactionEntity'
              AND OperationType = @operationType
              AND ClientReference = @clientReference
              AND PaymentIntentId LIKE 'pi[_]%'
            ORDER BY CreatedAt DESC
            """,
            new { operationType, clientReference });
        return Optional(value);
    }

    private static async Task<IResult> GetEscrowPayeeIdAsync(
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT ToOwnerId FROM payment.Escrows WHERE OperationType = @operationType AND ClientReference = @clientReference",
            new { operationType, clientReference });
        return Optional(value);
    }

    private static async Task<IResult> GetEscrowPaymentIntentIdAsync(
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<string>(
            "SELECT ChargeId FROM payment.Escrows WHERE OperationType = @operationType AND ClientReference = @clientReference",
            new { operationType, clientReference }));
    }

    private static async Task<IResult> GetEscrowStatusAsync(
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT Status FROM payment.Escrows WHERE OperationType = @operationType AND ClientReference = @clientReference",
            new { operationType, clientReference });
        return Optional(value);
    }

    private static async Task<IResult> GetEscrowRefundIdAsync(
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT TOP 1 r.StripeRefundId
            FROM payment.PaymentRefunds r
            JOIN payment.Escrows e ON e.Id = r.EscrowId
            WHERE e.OperationType = @operationType
              AND e.ClientReference = @clientReference
              AND r.StripeRefundId IS NOT NULL
            ORDER BY r.CompletedAt DESC
            """,
            new { operationType, clientReference });
        return Optional(value);
    }

    private static async Task<IResult> GetLedgerTransactionCountAsync(
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM payment.LedgerTransactions WHERE OperationType = @operationType AND ClientReference = @clientReference",
            new { operationType, clientReference }));
    }

    private static async Task<IResult> GetLedgerSignedSumAsync(
        string operationType,
        string clientReference,
        E2EAdminOptions options,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(options, cancellationToken);
        return Results.Ok(await connection.QuerySingleAsync<long>(
            """
            SELECT COALESCE(SUM(e.Amount), 0)
            FROM payment.LedgerEntries e
            JOIN payment.LedgerTransactions t ON t.Id = e.LedgerTransactionId
            WHERE t.OperationType = @operationType AND t.ClientReference = @clientReference
            """,
            new { operationType, clientReference }));
    }

    private static async Task<IResult> GetLedgerPlatformRevenueAsync(
        string operationType,
        string clientReference,
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
            WHERE t.OperationType = @operationType
              AND t.ClientReference = @clientReference
              AND a.Type = @platformRevenue
            """,
            new { operationType, clientReference, platformRevenue = PlatformRevenueAccountType }));
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

internal static class E2EAdminSecurity
{
    private const string AdminKeyHeader = "X-Concertable-E2E-Key";

    public static void RequireE2EEnvironment(IHostEnvironment environment)
    {
        if (!environment.IsE2E())
            throw new InvalidOperationException("Payment E2E admin endpoints can only be enabled in the E2E environment.");
    }

    public static bool IsAuthorized(string expected, IHeaderDictionary headers)
    {
        if (string.IsNullOrWhiteSpace(expected)
            || !headers.TryGetValue(AdminKeyHeader, out var suppliedValues))
        {
            return false;
        }

        var supplied = suppliedValues.ToString();
        if (string.IsNullOrWhiteSpace(supplied))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

internal sealed record E2EAdminOptions(string AdminKey, string ConnectionString);
