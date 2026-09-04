using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;
using Stripe;
using Transfer = Concertable.Payment.Contracts.Transfer;
using Refund = Concertable.Payment.Contracts.Refund;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripeTransferClient : IStripeTransferClient
{
    private readonly IStripeApiClient stripeClient;
    private readonly ILogger<StripeTransferClient> logger;

    public StripeTransferClient(IStripeApiClient stripeClient, ILogger<StripeTransferClient> logger)
    {
        this.stripeClient = stripeClient;
        this.logger = logger;
    }

    public async Task<Result<Transfer, PaymentError>> ReleaseAsync(
        StripeReleaseOptions opts,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result<Transfer, PaymentError>.Failure(new PaymentError.RecipientUnavailable());

            var transfer = await stripeClient.CreateTransferAsync(
                new TransferCreateOptions
                {
                    Amount = opts.Amount.ToMinorUnits(),
                    Currency = "GBP",
                    Destination = opts.DestinationStripeId,
                    SourceTransaction = opts.ChargeId,
                    Metadata = opts.Metadata
                },
                StripeRequestOptions.Release(opts.OperationId, opts.CommissionBindingId),
                ct);

            logger.StripeEscrowReleaseSucceeded(transfer.Id, transfer.Amount, opts.DestinationStripeId, opts.ChargeId);

            return Result<Transfer, PaymentError>.Success(new Transfer(transfer.Id));
        }
        catch (StripeException ex)
        {
            logger.StripeReleaseFailed(opts.Amount.ToMinorUnits(), opts.DestinationStripeId, opts.ChargeId, ex.StripeError?.Code, ex);
            if (StripeFailureClassifier.Classify(ex).TryGetValue(out var rejection))
                return Result<Transfer, PaymentError>.Failure(rejection.Error);
            throw;
        }
    }

    public async Task<Result<Refund, PaymentError>> RefundAsync(
        StripeRefundOptions opts,
        CancellationToken ct = default)
    {
        try
        {
            if (opts.TransferReversal is not null)
            {
                await stripeClient.CreateTransferReversalAsync(
                    opts.TransferReversal.TransferId,
                    new TransferReversalCreateOptions
                    {
                        Amount = opts.TransferReversal.Amount.ToMinorUnits(),
                        Metadata = opts.Metadata
                    },
                    StripeRequestOptions.RefundReversal(
                        opts.OperationId,
                        opts.CommissionBindingId,
                        opts.RefundId),
                    ct);

                logger.StripeTransferReversalSucceeded(
                    opts.TransferReversal.TransferId,
                    opts.TransferReversal.Amount.ToMinorUnits());
            }

            var refund = await stripeClient.CreateRefundAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = opts.PaymentIntentId,
                    Amount = opts.Amount.ToMinorUnits(),
                    ReverseTransfer = opts.ReverseTransfer ? true : null,
                    Reason = opts.Reason,
                    Metadata = opts.Metadata
                },
                StripeRequestOptions.Refund(
                    opts.OperationId,
                    opts.CommissionBindingId,
                    opts.RefundId),
                ct);

            logger.StripeRefundSucceeded(refund.Id, opts.PaymentIntentId, refund.Amount);

            return Result<Refund, PaymentError>.Success(new Refund(refund.Id));
        }
        catch (StripeException ex)
        {
            logger.StripeRefundFailed(opts.PaymentIntentId, ex.StripeError?.Code, ex);
            if (StripeFailureClassifier.Classify(ex).TryGetValue(out var rejection))
                return Result<Refund, PaymentError>.Failure(rejection.Error);
            throw;
        }
    }
}
