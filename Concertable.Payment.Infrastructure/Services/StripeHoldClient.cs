using Concertable.Payment.Application.Interfaces;
using Concertable.Shared.Exceptions;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class StripeHoldClient : IStripeHoldClient
{
    private readonly PaymentIntentService paymentIntentService;

    public StripeHoldClient(PaymentIntentService paymentIntentService)
    {
        this.paymentIntentService = paymentIntentService;
    }

    public async Task<string> FindHeldIntentAsync(string stripeCustomerId, int applicationId, CancellationToken ct = default)
    {
        var intents = await paymentIntentService.ListAsync(new PaymentIntentListOptions
        {
            Customer = stripeCustomerId,
            Limit = 10,
        }, cancellationToken: ct);

        var held = intents.FirstOrDefault(pi =>
            pi.Status == "requires_capture" &&
            pi.Metadata.TryGetValue("applicationId", out var id) &&
            id == applicationId.ToString());

        return held?.Id ?? throw new NotFoundException($"No held payment intent found for application {applicationId}");
    }

    public Task CancelAsync(string intentId, CancellationToken ct = default) =>
        paymentIntentService.CancelAsync(intentId, cancellationToken: ct);

    public Task CaptureAsync(string intentId, IDictionary<string, string> metadata, CancellationToken ct = default) =>
        paymentIntentService.CaptureAsync(intentId, new PaymentIntentCaptureOptions
        {
            Metadata = metadata.ToDictionary(kv => kv.Key, kv => kv.Value)
        }, cancellationToken: ct);
}
