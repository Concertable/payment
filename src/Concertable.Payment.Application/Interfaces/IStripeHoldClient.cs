namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeHoldClient
{
    Task<string> FindHeldIntentAsync(string stripeCustomerId, int applicationId, CancellationToken ct = default);
    Task CancelAsync(string intentId, CancellationToken ct = default);
    Task CaptureAsync(string intentId, IDictionary<string, string> metadata, CancellationToken ct = default);
}
