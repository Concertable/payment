using Reunion;
using Concertable.Payment.Client.Enums;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class PayoutAccountClient : IPayoutAccountOperationsClient
{
    private readonly Proto.PayoutAccount.PayoutAccountClient client;

    public PayoutAccountClient(Proto.PayoutAccount.PayoutAccountClient client)
    {
        this.client = client;
    }

    public async Task<Option<string>> GetOnboardingLinkAsync(
        Guid ownerId,
        CancellationToken ct = default)
    {
        var response = await client.GetOnboardingLinkAsync(Request(ownerId), cancellationToken: ct);
        return string.IsNullOrEmpty(response.Url)
            ? Option.None<string>()
            : Option.Some(response.Url);
    }

    public async Task<PayoutAccountStatus> GetAccountStatusAsync(
        Guid ownerId,
        CancellationToken ct = default)
    {
        var response = await client.GetAccountStatusAsync(Request(ownerId), cancellationToken: ct);
        return response.Status.ToStatus();
    }

    public async Task<Option<SavedCard>> GetPaymentMethodAsync(
        Guid ownerId,
        CancellationToken ct = default)
    {
        var response = await client.GetPaymentMethodAsync(Request(ownerId), cancellationToken: ct);
        return response.HasCard
            ? Option.Some(new SavedCard(response.Brand, response.Last4, response.ExpMonth, response.ExpYear))
            : Option.None<SavedCard>();
    }

    public async Task<Option<string>> CreateSetupIntentAsync(
        Guid ownerId,
        CancellationToken ct = default)
    {
        var response = await client.CreateSetupIntentAsync(Request(ownerId), cancellationToken: ct);
        return string.IsNullOrEmpty(response.ClientSecret)
            ? Option.None<string>()
            : Option.Some(response.ClientSecret);
    }

    private static Proto.PayoutOwnerRequest Request(Guid ownerId) =>
        Proto.PayoutOwnerRequest.Create(ownerId);
}
