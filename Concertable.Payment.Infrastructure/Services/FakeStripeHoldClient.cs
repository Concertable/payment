using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Services;

internal class FakeStripeHoldClient : IStripeHoldClient
{
    public Task<string> FindHeldIntentAsync(string stripeCustomerId, int applicationId, CancellationToken ct = default) =>
        Task.FromResult("pi_fake_hold_id");

    public Task CancelAsync(string intentId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task CaptureAsync(string intentId, IDictionary<string, string> metadata, CancellationToken ct = default) =>
        Task.CompletedTask;
}
