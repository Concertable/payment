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
        int bookingId,
        CancellationToken cancellationToken = default) =>
        GetOptionalStringAsync($"/_e2e/bookings/{bookingId}/settlement-payment-intent-id", cancellationToken);

    public Task<Guid?> GetEscrowPayeeIdAsync(int bookingId, CancellationToken cancellationToken = default) =>
        GetOptionalAsync<Guid>($"/_e2e/bookings/{bookingId}/escrow-payee-id", cancellationToken);

    public async Task<string> GetEscrowPaymentIntentIdAsync(
        int bookingId,
        CancellationToken cancellationToken = default) =>
        await client.GetFromJsonAsync<string>(
            $"/_e2e/bookings/{bookingId}/escrow-payment-intent-id",
            cancellationToken)
            ?? throw new InvalidOperationException($"Booking {bookingId} has no escrow payment intent.");

    public Task<int?> GetEscrowStatusAsync(int bookingId, CancellationToken cancellationToken = default) =>
        GetOptionalAsync<int>($"/_e2e/bookings/{bookingId}/escrow-status", cancellationToken);

    public Task<string?> GetEscrowRefundIdAsync(int bookingId, CancellationToken cancellationToken = default) =>
        GetOptionalStringAsync($"/_e2e/bookings/{bookingId}/escrow-refund-id", cancellationToken);

    public Task<int> GetLedgerTransactionCountAsync(int bookingId, CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<int>($"/_e2e/bookings/{bookingId}/ledger-transaction-count", cancellationToken);

    public Task<long> GetLedgerSignedSumAsync(int bookingId, CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<long>($"/_e2e/bookings/{bookingId}/ledger-signed-sum", cancellationToken);

    public Task<long> GetLedgerPlatformRevenueAsync(int bookingId, CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<long>($"/_e2e/bookings/{bookingId}/ledger-platform-revenue", cancellationToken);

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
