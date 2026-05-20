using FluentResults;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class FakeStripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IWebhookQueue webhookQueue;

    public FakeStripePaymentIntentClient(IWebhookQueue webhookQueue)
    {
        this.webhookQueue = webhookQueue;
    }

    public async Task<Result<PaymentResponse>> ChargeAsync(StripeChargeOptions opts)
    {
        var transactionId = $"pi_fake_{Guid.NewGuid():N}";

        await webhookQueue.EnqueueAsync(new Event
        {
            Id = $"evt_fake_{Guid.NewGuid():N}",
            Type = "payment_intent.succeeded",
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = transactionId,
                    Status = "succeeded",
                    AmountReceived = (long)(opts.Amount * 100),
                    Metadata = opts.Metadata
                }
            }
        });

        return Result.Ok(new PaymentResponse
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }

    public async Task<Result<PaymentResponse>> HoldAsync(StripeHoldOptions opts)
    {
        var transactionId = $"pi_fake_{Guid.NewGuid():N}";

        await webhookQueue.EnqueueAsync(new Event
        {
            Id = $"evt_fake_{Guid.NewGuid():N}",
            Type = "payment_intent.succeeded",
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = transactionId,
                    Status = "succeeded",
                    AmountReceived = (long)(opts.Amount * 100),
                    Metadata = opts.Metadata
                }
            }
        });

        return Result.Ok(new PaymentResponse
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }

    public Task<Result<TransferResponse>> ReleaseAsync(StripeReleaseOptions opts) =>
        Task.FromResult(Result.Ok(new TransferResponse($"tr_fake_{Guid.NewGuid():N}")));

    public Task<Result<RefundResponse>> RefundAsync(StripeRefundOptions opts) =>
        Task.FromResult(Result.Ok(new RefundResponse($"re_fake_{Guid.NewGuid():N}")));
}
