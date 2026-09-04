using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IWebhookQueue webhookQueue;

    public FakeStripePaymentIntentClient(IWebhookQueue webhookQueue)
    {
        this.webhookQueue = webhookQueue;
    }

    public async Task<Result<PaymentOutcome, PaymentRejection>> ChargeAsync(
        StripeChargeOptions options,
        CancellationToken ct = default) =>
        (await CompleteAsync(options.Amount, options.Metadata, ct)).MapError(PaymentRejection.Declined);

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
                    Status = StripePaymentIntentStatuses.Succeeded,
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
