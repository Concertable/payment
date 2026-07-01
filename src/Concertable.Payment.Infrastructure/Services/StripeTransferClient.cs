using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using FluentResults;
using Microsoft.Extensions.Logging;
using Stripe;
using Transfer = Concertable.Payment.Application.DTOs.Transfer;
using Refund = Concertable.Payment.Application.DTOs.Refund;

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

    public async Task<Result<Transfer>> ReleaseAsync(StripeReleaseOptions opts)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result.Fail("Recipient does not have a Stripe account");

            var transfer = await stripeClient.CreateTransferAsync(new TransferCreateOptions
            {
                Amount = (long)(opts.Amount * 100),
                Currency = "GBP",
                Destination = opts.DestinationStripeId,
                SourceTransaction = opts.ChargeId,
                Metadata = opts.Metadata
            });

            logger.StripeEscrowReleaseSucceeded(transfer.Id, transfer.Amount, opts.DestinationStripeId, opts.ChargeId);

            return Result.Ok(new Transfer(transfer.Id));
        }
        catch (StripeException ex)
        {
            logger.StripeReleaseFailed((long)(opts.Amount * 100), opts.DestinationStripeId, opts.ChargeId, ex.StripeError?.Code, ex);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.ReleaseProcessingFailed((long)(opts.Amount * 100), opts.DestinationStripeId, ex);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }

    public async Task<Result<Refund>> RefundAsync(StripeRefundOptions opts)
    {
        try
        {
            if (!string.IsNullOrEmpty(opts.TransferId))
            {
                await stripeClient.CreateTransferReversalAsync(opts.TransferId, new TransferReversalCreateOptions
                {
                    Amount = (long)(opts.Amount * 100),
                    Metadata = opts.Metadata
                });

                logger.StripeTransferReversalSucceeded(opts.TransferId, (long)(opts.Amount * 100));
            }

            var refund = await stripeClient.CreateRefundAsync(new RefundCreateOptions
            {
                PaymentIntent = opts.PaymentIntentId,
                Amount = (long)(opts.Amount * 100),
                Reason = opts.Reason,
                Metadata = opts.Metadata
            });

            logger.StripeRefundSucceeded(refund.Id, opts.PaymentIntentId, refund.Amount);

            return Result.Ok(new Refund(refund.Id));
        }
        catch (StripeException ex)
        {
            logger.StripeRefundFailed(opts.PaymentIntentId, ex.StripeError?.Code, ex);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.RefundProcessingFailed(opts.PaymentIntentId, ex);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }
}
