using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripeHoldClient : IStripeHoldClient
{
    public Task<string> FindHeldIntentAsync(string stripeCustomerId, int applicationId, CancellationToken ct = default) =>
        Task.FromResult("pi_fake_hold_id");

    public Task CaptureAsync(string intentId, IReadOnlyDictionary<string, string> metadata, CancellationToken ct = default) =>
        Task.CompletedTask;
}
