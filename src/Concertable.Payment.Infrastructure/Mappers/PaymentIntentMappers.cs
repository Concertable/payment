using Concertable.Payment.Application.DTOs;
using Stripe;

namespace Concertable.Payment.Infrastructure.Mappers;

internal static class PaymentIntentMappers
{
    public static Result<PaymentOutcome, PaymentError> ToPaymentResult(this PaymentIntent intent) =>
        intent.Status is not ("succeeded" or "requires_action" or "requires_confirmation")
            ? Result<PaymentOutcome, PaymentError>.Failure(PaymentError.Rejected())
            : Result<PaymentOutcome, PaymentError>.Success(new PaymentOutcome
            {
                RequiresAction = intent.Status is "requires_action" or "requires_confirmation",
                ClientSecret = intent.ClientSecret,
                TransactionId = intent.Id
            });
}
