using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Application.Requests;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Mappers;
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

    public async Task<Result<ProviderPaymentOutcome, ChargeError>> ChargeAsync(
        StripeChargeOptions opts,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result<ProviderPaymentOutcome, ChargeError>.Failure(
                    new ChargeError.PaymentFailure(new PaymentError.RecipientUnavailable()));

            if (await stripeAccountClient.GetAccountStatusAsync(opts.DestinationStripeId) != PayoutAccountStatus.Verified)
                return Result<ProviderPaymentOutcome, ChargeError>.Failure(
                    new ChargeError.PaymentFailure(new PaymentError.RecipientUnavailable()));

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
                StripeRequestOptions.Charge(opts.OperationId, opts.CommissionBindingId),
                ct);

            if (paymentIntent.Status == StripePaymentIntentStatuses.Succeeded)
                logger.StripePaymentIntentSucceeded(paymentIntent.Id, paymentIntent.Amount, options.TransferData.Destination);
            else
                logger.StripePaymentIntentNonSucceeded(paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.TransferData.Destination);

            return paymentIntent.ToPaymentResult()
                .MapError(ChargeError (error) => new ChargeError.PaymentFailure(error));
        }
        catch (StripeException ex)
        {
            logger.StripeChargeFailed(opts.Amount.ToMinorUnits(), opts.DestinationStripeId, ex.StripeError?.Code, ex);
            if (StripeFailureClassifier.Classify(ex).TryGetValue(out var rejection))
                return Result<ProviderPaymentOutcome, ChargeError>.Failure(rejection);
            throw;
        }
    }

    public async Task<Result<ProviderPaymentOutcome, PaymentError>> HoldAsync(
        StripeHoldOptions opts,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(opts.DestinationStripeId))
                return Result<ProviderPaymentOutcome, PaymentError>.Failure(new PaymentError.RecipientUnavailable());

            if (await stripeAccountClient.GetAccountStatusAsync(opts.DestinationStripeId) != PayoutAccountStatus.Verified)
                return Result<ProviderPaymentOutcome, PaymentError>.Failure(new PaymentError.RecipientUnavailable());

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
                StripeRequestOptions.Deposit(opts.OperationId, opts.CommissionBindingId),
                ct);

            if (paymentIntent.Status == StripePaymentIntentStatuses.Succeeded)
                logger.StripeEscrowHoldSucceeded(paymentIntent.Id, paymentIntent.Amount, options.OnBehalfOf);
            else
                logger.StripeEscrowHoldNonSucceeded(paymentIntent.Id, paymentIntent.Status, paymentIntent.Amount, options.OnBehalfOf);

            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            logger.StripeHoldFailed(opts.Amount.ToMinorUnits(), opts.DestinationStripeId, ex.StripeError?.Code, ex);
            if (StripeFailureClassifier.Classify(ex).TryGetValue(out var rejection))
                return Result<ProviderPaymentOutcome, PaymentError>.Failure(rejection.ToPaymentError());
            throw;
        }
    }

    public async Task<Result<ProviderPaymentOutcome, PaymentError>> GetAsync(
        string paymentIntentId,
        CancellationToken ct = default)
    {
        try
        {
            var paymentIntent = await stripeClient.GetPaymentIntentAsync(paymentIntentId, ct);
            return paymentIntent.ToPaymentResult();
        }
        catch (StripeException ex)
        {
            if (StripeFailureClassifier.Classify(ex).TryGetValue(out var rejection))
                return Result<ProviderPaymentOutcome, PaymentError>.Failure(rejection.ToPaymentError());
            throw;
        }
    }
}
