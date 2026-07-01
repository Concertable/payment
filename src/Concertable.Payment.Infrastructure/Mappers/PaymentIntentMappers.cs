using Concertable.Payment.Application.DTOs;
using FluentResults;
using Stripe;

namespace Concertable.Payment.Infrastructure.Mappers;

internal static class PaymentIntentMappers
{
    public static Result<PaymentOutcome> ToPaymentResult(this PaymentIntent intent) =>
        intent.Status is not ("succeeded" or "requires_action" or "requires_confirmation")
            ? Result.Fail($"Payment failed with status: {intent.Status}")
            : Result.Ok(new PaymentOutcome
            {
                RequiresAction = intent.Status is "requires_action" or "requires_confirmation",
                ClientSecret = intent.ClientSecret,
                TransactionId = intent.Id
            });
}
