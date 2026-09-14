using Concertable.Payment.Application.DTOs;
using Reunion;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Infrastructure;
using Stripe;

namespace Concertable.Payment.Infrastructure.Mappers;

internal static class PaymentIntentMappers
{
    extension(PaymentIntent intent)
    {
        public Result<ProviderPaymentOutcome, PaymentError> ToPaymentResult()
        {
            if (intent.Status is not (StripePaymentIntentStatuses.Succeeded
                    or StripePaymentIntentStatuses.RequiresAction
                    or StripePaymentIntentStatuses.RequiresConfirmation))
                return Result.Failure<ProviderPaymentOutcome, PaymentError>(new PaymentError.PaymentRejected());
            if (string.IsNullOrEmpty(intent.Id))
                throw new InvalidOperationException("Stripe response missing PaymentIntent id.");

            return Result.Success<ProviderPaymentOutcome, PaymentError>(new(
                intent.Id,
                intent.Status is StripePaymentIntentStatuses.RequiresAction
                    or StripePaymentIntentStatuses.RequiresConfirmation,
                intent.ClientSecret));
        }
    }
}
