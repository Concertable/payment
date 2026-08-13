namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeHoldClient
{
    Task<string> FindHeldIntentAsync(string stripeCustomerId, int applicationId, CancellationToken ct = default);
    Task CaptureAsync(
        string intentId,
        IReadOnlyDictionary<string, string> metadata,
        Guid? operationId,
        Guid? commissionBindingId,
        CancellationToken ct = default);
}
