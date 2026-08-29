using Concertable.Payment.Application.DTOs;
using Reunion;
using Concertable.Payment.Contracts.Errors;
using Stripe;

namespace Concertable.Payment.Infrastructure.Mappers;

internal static class PaymentIntentMappers
{
    public static Result<PaymentOutcome, PaymentError> ToPaymentResult(this PaymentIntent intent)
    {
        if (intent.Status is not (StripePaymentIntentStatus.Succeeded
                or StripePaymentIntentStatus.RequiresAction
                or StripePaymentIntentStatus.RequiresConfirmation))
            return Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PaymentRejected());
        if (string.IsNullOrEmpty(intent.Id))
            throw new InvalidOperationException("Stripe response missing PaymentIntent id.");

        return Result.Success<PaymentOutcome, PaymentError>(new PaymentOutcome
        {
            RequiresAction = intent.Status
                is StripePaymentIntentStatus.RequiresAction or StripePaymentIntentStatus.RequiresConfirmation,
            ClientSecret = intent.ClientSecret,
            TransactionId = intent.Id
        });
    }
}
