using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class PayoutAccountClient : IPayoutAccountClient
{
    private readonly Proto.PayoutAccount.PayoutAccountClient client;

    public PayoutAccountClient(Proto.PayoutAccount.PayoutAccountClient client)
    {
        this.client = client;
    }

    public async Task<string?> GetOnboardingLinkAsync(Guid ownerId, CancellationToken ct = default)
    {
        var response = await client.GetOnboardingLinkAsync(Request(ownerId), cancellationToken: ct);
        return string.IsNullOrEmpty(response.Url) ? null : response.Url;
    }

    public async Task<PayoutAccountStatus> GetAccountStatusAsync(Guid ownerId, CancellationToken ct = default)
    {
        var response = await client.GetAccountStatusAsync(Request(ownerId), cancellationToken: ct);
        return response.Status.ToStatus();
    }

    public async Task<SavedCard?> GetPaymentMethodAsync(Guid ownerId, CancellationToken ct = default)
    {
        var response = await client.GetPaymentMethodAsync(Request(ownerId), cancellationToken: ct);
        return response.HasCard
            ? new SavedCard(response.Brand, response.Last4, response.ExpMonth, response.ExpYear)
            : null;
    }

    public async Task<string?> CreateSetupIntentAsync(Guid ownerId, CancellationToken ct = default)
    {
        var response = await client.CreateSetupIntentAsync(Request(ownerId), cancellationToken: ct);
        return string.IsNullOrEmpty(response.ClientSecret) ? null : response.ClientSecret;
    }

    private static Proto.PayoutOwnerRequest Request(Guid ownerId) => new() { OwnerId = ownerId.ToString() };
}
