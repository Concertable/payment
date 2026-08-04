using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Mappers;
using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IStripeApiClient stripeClient;
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly IPaymentSessionConfigurator configurator;
    private readonly ILogger<StripePaymentIntentClient> logger;

    public StripePaymentIntentClient(
        IStripeApiClient stripeClient,
        IStripeAccountClient stripeAccountClient,
        IPaymentSessionConfigurator configurator,
        ILogger<StripePaymentIntentClient> logger)
    {
        this.stripeClient = stripeClient;
        this.stripeAccountClient = stripeAccountClient;
        this.configurator = configurator;
        this.logger = logger;
    }

    public async Task<Result<PaymentOutcome, PaymentError>> ChargeAsync(StripeChargeOptions opts)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.RecipientUnavailable());

            if (await stripeAccountClient.GetAccountStatusAsync(opts.DestinationStripeId) != PayoutAccountStatus.Verified)
                return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.RecipientUnavailable());

            var options = new PaymentIntentCreateOptions
            {
                Amount = opts.Amount.ToMinorUnits(),
                Currency = "GBP",
                PaymentMethod = opts.PaymentMethodId,
                Customer = opts.StripeCustomerId,
                Confirm = true,
                PaymentMethodTypes = ["card"],
                ReceiptEmail = opts.ReceiptEmail,
                Metadata = opts.Metadata,
                TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = opts.DestinationStripeId,
                    Amount = (opts.TransferAmount ?? opts.Amount).ToMinorUnits()
                }
            };

            configurator.Configure(options);

            var paymentIntent = await stripeClient.CreatePaymentIntentAsync(
                options,
                StripeIdempotency.FromMetadata(opts.Metadata, "charge"));

            if (paymentIntent.Status == "succeeded")
                logger.StripePaymentIntentSucceeded(paymentIntent.Id, paymentIntent.Amount, options.TransferData.Destination);
            else
                logger.StripePaymentIntentNonSucceeded(paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.TransferData.Destination);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.StripeChargeFailed(opts.Amount.ToMinorUnits(), opts.DestinationStripeId, ex.StripeError?.Code, ex);
            if (ex.StripeError?.Type == "card_error")
                return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PaymentRejected());
            throw;
        }
    }

    public async Task<Result<PaymentOutcome, PaymentError>> HoldAsync(StripeHoldOptions opts)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.RecipientUnavailable());

            if (await stripeAccountClient.GetAccountStatusAsync(opts.DestinationStripeId) != PayoutAccountStatus.Verified)
                return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.RecipientUnavailable());

            var options = new PaymentIntentCreateOptions
            {
                Amount = opts.Amount.ToMinorUnits(),
                Currency = "GBP",
                PaymentMethod = opts.PaymentMethodId,
                Customer = opts.StripeCustomerId,
                Confirm = true,
                PaymentMethodTypes = ["card"],
                ReceiptEmail = opts.ReceiptEmail,
                Metadata = opts.Metadata,
                OnBehalfOf = opts.DestinationStripeId
            };

            configurator.Configure(options);

            var paymentIntent = await stripeClient.CreatePaymentIntentAsync(
                options,
                StripeIdempotency.FromMetadata(opts.Metadata, "hold"));

            if (paymentIntent.Status == "succeeded")
                logger.StripeEscrowHoldSucceeded(paymentIntent.Id, paymentIntent.Amount, options.OnBehalfOf);
            else
                logger.StripeEscrowHoldNonSucceeded(paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.OnBehalfOf);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.StripeHoldFailed(opts.Amount.ToMinorUnits(), opts.DestinationStripeId, ex.StripeError?.Code, ex);
            if (ex.StripeError?.Type == "card_error")
                return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PaymentRejected());
            throw;
        }
    }
}
