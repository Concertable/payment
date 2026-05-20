using Concertable.Payment.Application.Requests;
using FluentResults;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class StripeTransferClient : IStripeTransferClient
{
    private readonly IStripeApiClient stripeClient;
    private readonly ILogger<StripeTransferClient> logger;

    public StripeTransferClient(IStripeApiClient stripeClient, ILogger<StripeTransferClient> logger)
    {
        this.stripeClient = stripeClient;
        this.logger = logger;
    }

    public async Task<Result<TransferResponse>> ReleaseAsync(StripeReleaseOptions opts)
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

            logger.LogInformation(
                "Stripe escrow release {TransferId} succeeded: {AmountPence} pence to {Destination} from charge {ChargeId}",
                transfer.Id, transfer.Amount, opts.DestinationStripeId, opts.ChargeId);

            return Result.Ok(new TransferResponse(transfer.Id));
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe release failed for {AmountPence} pence to {Destination} from charge {ChargeId}: {StripeCode}",
                (long)(opts.Amount * 100), opts.DestinationStripeId, opts.ChargeId, ex.StripeError?.Code);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Release processing failed for {AmountPence} pence to {Destination}",
                (long)(opts.Amount * 100), opts.DestinationStripeId);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }

    public async Task<Result<RefundResponse>> RefundAsync(StripeRefundOptions opts)
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

                logger.LogInformation(
                    "Stripe transfer reversal succeeded for transfer {TransferId}: {AmountPence} pence",
                    opts.TransferId, (long)(opts.Amount * 100));
            }

            var refund = await stripeClient.CreateRefundAsync(new RefundCreateOptions
            {
                PaymentIntent = opts.PaymentIntentId,
                Amount = (long)(opts.Amount * 100),
                Reason = opts.Reason,
                Metadata = opts.Metadata
            });

            logger.LogInformation(
                "Stripe refund {RefundId} succeeded for payment intent {IntentId}: {AmountPence} pence",
                refund.Id, opts.PaymentIntentId, refund.Amount);

            return Result.Ok(new RefundResponse(refund.Id));
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe refund failed for payment intent {IntentId}: {StripeCode}",
                opts.PaymentIntentId, ex.StripeError?.Code);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Refund processing failed for payment intent {IntentId}",
                opts.PaymentIntentId);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }
}
