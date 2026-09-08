using System.Net.Http.Json;

namespace Concertable.Payment.TestKit;

public sealed class PaymentTestClient
{
    public const string AdminKeyHeader = "X-Concertable-E2E-Key";

    private readonly HttpClient client;

    public PaymentTestClient(HttpClient client, string adminKey)
    {
        this.client = client;
        this.client.DefaultRequestHeaders.Add(AdminKeyHeader, adminKey);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.PostAsync("/_e2e/reset", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<string?> GetLatestSettlementPaymentIntentIdAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        GetOptionalStringAsync(Path("settlement-payment-intent-id", operationType, clientReference), cancellationToken);

    public Task<Guid?> GetEscrowPayeeIdAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        GetOptionalAsync<Guid>(Path("escrow-payee-id", operationType, clientReference), cancellationToken);

    public async Task<string> GetEscrowPaymentIntentIdAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        await client.GetFromJsonAsync<string>(
            Path("escrow-payment-intent-id", operationType, clientReference),
            cancellationToken)
            ?? throw new InvalidOperationException($"Operation {operationType}/{clientReference} has no escrow payment intent.");

    public Task<int?> GetEscrowStatusAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        GetOptionalAsync<int>(Path("escrow-status", operationType, clientReference), cancellationToken);

    public Task<string?> GetEscrowRefundIdAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        GetOptionalStringAsync(Path("escrow-refund-id", operationType, clientReference), cancellationToken);

    public Task<int> GetLedgerTransactionCountAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<int>(Path("ledger-transaction-count", operationType, clientReference), cancellationToken);

    public Task<long> GetLedgerSignedSumAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<long>(Path("ledger-signed-sum", operationType, clientReference), cancellationToken);

    public Task<long> GetLedgerPlatformRevenueAsync(
        string operationType,
        string clientReference,
        CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<long>(Path("ledger-platform-revenue", operationType, clientReference), cancellationToken);

    public Task<int> GetActiveOutboxCountAsync(CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<int>("/_e2e/operations/active-outbox-count", cancellationToken);

    private static string Path(string resource, string operationType, string clientReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientReference);
        return $"/_e2e/operations/{resource}?operationType={Uri.EscapeDataString(operationType)}&clientReference={Uri.EscapeDataString(clientReference)}";
    }

    private async Task<T?> GetOptionalAsync<T>(string path, CancellationToken cancellationToken)
        where T : struct
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<string?> GetOptionalStringAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<string>(cancellationToken);
    }
}
