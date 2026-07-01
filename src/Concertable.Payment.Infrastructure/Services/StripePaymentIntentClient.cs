using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Mappers;
using FluentResults;
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

    public async Task<Result<PaymentOutcome>> ChargeAsync(StripeChargeOptions opts)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result.Fail("Recipient does not have a Stripe account");

            if (await stripeAccountClient.GetAccountStatusAsync(opts.DestinationStripeId) != PayoutAccountStatus.Verified)
                return Result.Fail("Recipient is not eligible for payouts");

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(opts.Amount * 100),
                Currency = "GBP",
                PaymentMethod = opts.PaymentMethodId,
                Customer = opts.StripeCustomerId,
                Confirm = true,
                PaymentMethodTypes = ["card"],
                ReceiptEmail = opts.ReceiptEmail,
                Metadata = opts.Metadata,
                TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = opts.DestinationStripeId
                }
            };

            configurator.Configure(options);

            var paymentIntent = await stripeClient.CreatePaymentIntentAsync(options);

            if (paymentIntent.Status == "succeeded")
                logger.StripePaymentIntentSucceeded(paymentIntent.Id, paymentIntent.Amount, options.TransferData.Destination);
            else
                logger.StripePaymentIntentNonSucceeded(paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.TransferData.Destination);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.StripeChargeFailed((long)(opts.Amount * 100), opts.DestinationStripeId, ex.StripeError?.Code, ex);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.PaymentProcessingFailed((long)(opts.Amount * 100), opts.DestinationStripeId, ex);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }

    public async Task<Result<PaymentOutcome>> HoldAsync(StripeHoldOptions opts)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result.Fail("Recipient does not have a Stripe account");

            if (await stripeAccountClient.GetAccountStatusAsync(opts.DestinationStripeId) != PayoutAccountStatus.Verified)
                return Result.Fail("Recipient is not eligible for payouts");

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(opts.Amount * 100),
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

            var paymentIntent = await stripeClient.CreatePaymentIntentAsync(options);

            if (paymentIntent.Status == "succeeded")
                logger.StripeEscrowHoldSucceeded(paymentIntent.Id, paymentIntent.Amount, options.OnBehalfOf);
            else
                logger.StripeEscrowHoldNonSucceeded(paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.OnBehalfOf);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.StripeHoldFailed((long)(opts.Amount * 100), opts.DestinationStripeId, ex.StripeError?.Code, ex);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.HoldProcessingFailed((long)(opts.Amount * 100), opts.DestinationStripeId, ex);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }
}
