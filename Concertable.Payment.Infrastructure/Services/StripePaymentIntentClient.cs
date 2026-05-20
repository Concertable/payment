using Concertable.Payment.Infrastructure.Mappers;
using FluentResults;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class StripePaymentIntentClient : IStripePaymentIntentClient
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

    public async Task<Result<PaymentResponse>> ChargeAsync(StripeChargeOptions opts)
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
                logger.LogInformation(
                    "Stripe payment intent {IntentId} succeeded: {AmountPence} pence to {Destination}",
                    paymentIntent.Id, paymentIntent.Amount, options.TransferData.Destination);
            else
                logger.LogWarning(
                    "Stripe payment intent {IntentId} returned non-succeeded status {Status}: {AmountPence} pence to {Destination}",
                    paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.TransferData.Destination);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe charge failed for {AmountPence} pence to {Destination}: {StripeCode}",
                (long)(opts.Amount * 100), opts.DestinationStripeId, ex.StripeError?.Code);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Payment processing failed for {AmountPence} pence to {Destination}",
                (long)(opts.Amount * 100), opts.DestinationStripeId);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }

    public async Task<Result<PaymentResponse>> HoldAsync(StripeHoldOptions opts)
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
                logger.LogInformation(
                    "Stripe escrow hold {IntentId} succeeded: {AmountPence} pence held in platform balance on behalf of {Destination}",
                    paymentIntent.Id, paymentIntent.Amount, options.OnBehalfOf);
            else
                logger.LogWarning(
                    "Stripe escrow hold {IntentId} returned non-succeeded status {Status}: {AmountPence} pence on behalf of {Destination}",
                    paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.OnBehalfOf);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe hold failed for {AmountPence} pence on behalf of {Destination}: {StripeCode}",
                (long)(opts.Amount * 100), opts.DestinationStripeId, ex.StripeError?.Code);
            return Result.Fail($"Stripe Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Hold processing failed for {AmountPence} pence on behalf of {Destination}",
                (long)(opts.Amount * 100), opts.DestinationStripeId);
            return Result.Fail($"General Error: {ex.Message}");
        }
    }
}
