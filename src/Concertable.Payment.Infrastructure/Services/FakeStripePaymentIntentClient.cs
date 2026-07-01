using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using FluentResults;
using Stripe;
using Transfer = Concertable.Payment.Application.DTOs.Transfer;
using Refund = Concertable.Payment.Application.DTOs.Refund;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IWebhookQueue webhookQueue;

    public FakeStripePaymentIntentClient(IWebhookQueue webhookQueue)
    {
        this.webhookQueue = webhookQueue;
    }

    public async Task<Result<PaymentOutcome>> ChargeAsync(StripeChargeOptions opts)
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

        return Result.Ok(new PaymentOutcome
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }

    public async Task<Result<PaymentOutcome>> HoldAsync(StripeHoldOptions opts)
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

        return Result.Ok(new PaymentOutcome
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }

    public Task<Result<Transfer>> ReleaseAsync(StripeReleaseOptions opts) =>
        Task.FromResult(Result.Ok(new Transfer($"tr_fake_{Guid.NewGuid():N}")));

    public Task<Result<Refund>> RefundAsync(StripeRefundOptions opts) =>
        Task.FromResult(Result.Ok(new Refund($"re_fake_{Guid.NewGuid():N}")));
}
