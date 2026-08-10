using Concertable.Payment.Application.DTOs;
using Reunion;
using Concertable.Payment.Contracts.Errors;
using Stripe;

namespace Concertable.Payment.Infrastructure.Mappers;

internal static class PaymentIntentMappers
{
    public static Result<PaymentOutcome, PaymentError> ToPaymentResult(this PaymentIntent intent) =>
        intent.Status is not ("succeeded" or "requires_action" or "requires_confirmation")
            ? Result.Failure<PaymentOutcome, PaymentError>(new PaymentError.PaymentRejected())
            : Result.Success<PaymentOutcome, PaymentError>(new PaymentOutcome
            {
                RequiresAction = intent.Status is "requires_action" or "requires_confirmation",
                ClientSecret = intent.ClientSecret,
                TransactionId = intent.Id
            });
}
