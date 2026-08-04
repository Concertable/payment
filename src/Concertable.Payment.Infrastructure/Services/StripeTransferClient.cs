using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
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

    public async Task<Result<Transfer, ReleaseError>> ReleaseAsync(StripeReleaseOptions opts)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result.Failure<Transfer, ReleaseError>(ReleaseError.RecipientUnavailable);

            var transfer = await stripeClient.CreateTransferAsync(
                new TransferCreateOptions
                {
                    Amount = opts.Amount.ToMinorUnits(),
                    Currency = "GBP",
                    Destination = opts.DestinationStripeId,
                    SourceTransaction = opts.ChargeId,
                    Metadata = opts.Metadata
                },
                StripeIdempotency.FromMetadata(opts.Metadata, "release"));

            logger.StripeEscrowReleaseSucceeded(transfer.Id, transfer.Amount, opts.DestinationStripeId, opts.ChargeId);

            return Result.Success<Transfer, ReleaseError>(new Transfer(transfer.Id));
        }
        catch (StripeException ex)
        {
            logger.StripeReleaseFailed(opts.Amount.ToMinorUnits(), opts.DestinationStripeId, opts.ChargeId, ex.StripeError?.Code, ex);
            if (ex.StripeError?.Type is "card_error" or "invalid_request_error")
                return Result.Failure<Transfer, ReleaseError>(ReleaseError.ReleaseRejected);
            throw;
        }
    }

    public async Task<Result<Refund, RefundError>> RefundAsync(StripeRefundOptions opts)
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
                    StripeIdempotency.FromMetadata(
                        opts.Metadata,
                        $"refund-reversal:{opts.Metadata.GetValue(PaymentMetadataKeys.CumulativeGrossRefundMinor)}"));

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
                StripeIdempotency.FromMetadata(
                    opts.Metadata,
                    $"refund:{opts.Metadata.GetValue(PaymentMetadataKeys.CumulativeGrossRefundMinor)}"));

            logger.StripeRefundSucceeded(refund.Id, opts.PaymentIntentId, refund.Amount);

            return Result.Success<Refund, RefundError>(new Refund(refund.Id));
        }
        catch (StripeException ex)
        {
            logger.StripeRefundFailed(opts.PaymentIntentId, ex.StripeError?.Code, ex);
            if (ex.StripeError?.Type is "card_error" or "invalid_request_error")
                return Result.Failure<Refund, RefundError>(RefundError.RefundRejected);
            throw;
        }
    }
}
