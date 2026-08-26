using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IWebhookQueue webhookQueue;

    public FakeStripePaymentIntentClient(IWebhookQueue webhookQueue)
    {
        this.webhookQueue = webhookQueue;
    }

    public Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(
        StripeChargeOptions options,
        CancellationToken ct = default) =>
        CompleteAsync(options.Amount, options.Metadata, ct);

    public Task<Result<PaymentOutcome, PaymentError>> HoldAsync(
        StripeHoldOptions options,
        CancellationToken ct = default) =>
        CompleteAsync(options.Amount, options.Metadata, ct);

    public Task<Result<PaymentOutcome, PaymentError>> GetAsync(
        string paymentIntentId,
        CancellationToken ct = default) =>
        Task.FromResult(Result<PaymentOutcome, PaymentError>.Success(new PaymentOutcome
        {
            TransactionId = paymentIntentId
        }));

    private async Task<Result<PaymentOutcome, PaymentError>> CompleteAsync(
        Money amount,
        Dictionary<string, string> metadata,
        CancellationToken ct)
    {
        var transactionId = $"pi_fake_{Guid.NewGuid():N}";
        await webhookQueue.EnqueueAsync(new Event
        {
            Id = $"evt_fake_{Guid.NewGuid():N}",
            Type = EventTypes.PaymentIntentSucceeded,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = transactionId,
                    Status = "succeeded",
                    AmountReceived = amount.ToMinorUnits(),
                    Metadata = metadata
                }
            }
        });

        return Result<PaymentOutcome, PaymentError>.Success(new PaymentOutcome
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }
}
