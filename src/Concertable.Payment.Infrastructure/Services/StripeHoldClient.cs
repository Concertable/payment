using Concertable.Payment.Application.Interfaces;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripeHoldClient : IStripeHoldClient
{
    private readonly PaymentIntentService paymentIntentService;

    public StripeHoldClient(PaymentIntentService paymentIntentService)
    {
        this.paymentIntentService = paymentIntentService;
    }

    public Task CaptureAsync(
        string intentId,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        Guid? commissionBindingId,
        CancellationToken ct = default) =>
        paymentIntentService.CaptureAsync(
            intentId,
            new PaymentIntentCaptureOptions
            {
                Metadata = metadata.ToDictionary(pair => pair.Key, pair => pair.Value)
            },
            StripeRequestOptions.Capture(operationId, commissionBindingId),
            ct);
}
