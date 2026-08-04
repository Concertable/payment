using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class FakeStripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IWebhookQueue webhookQueue;

    public FakeStripePaymentIntentClient(IWebhookQueue webhookQueue)
    {
        this.webhookQueue = webhookQueue;
    }

    public async Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(StripeChargeOptions opts)
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

        return Result.Success<PaymentOutcome, PaymentError>(new PaymentOutcome
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }

    public async Task<Result<PaymentOutcome, PaymentError>> HoldAsync(StripeHoldOptions opts)
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
                    AmountReceived = opts.Amount.ToMinorUnits(),
                    Metadata = opts.Metadata
                }
            }
        });

        return Result.Success<PaymentOutcome, PaymentError>(new PaymentOutcome
        {
            RequiresAction = false,
            TransactionId = transactionId
        });
    }

    public Task<Result<Transfer, ReleaseError>> ReleaseAsync(StripeReleaseOptions opts) =>
        Task.FromResult(Result.Success<Transfer, ReleaseError>(new Transfer($"tr_fake_{Guid.NewGuid():N}")));

    public Task<Result<Refund, RefundError>> RefundAsync(StripeRefundOptions opts) =>
        Task.FromResult(Result.Success<Refund, RefundError>(new Refund($"re_fake_{Guid.NewGuid():N}")));
}
