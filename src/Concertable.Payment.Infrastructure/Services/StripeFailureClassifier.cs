using System.Net;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeFailureClassifier
{
    public static Option<PaymentError> Classify(StripeException exception)
    {
        if (exception.HttpStatusCode == HttpStatusCode.PaymentRequired ||
            string.Equals(exception.StripeError?.Type, "card_error", StringComparison.Ordinal))
            return Option.Some(PaymentError.Declined());

        if (exception.HttpStatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.NotFound
            or HttpStatusCode.Conflict
            or HttpStatusCode.UnprocessableEntity)
            return Option.Some(PaymentError.Rejected());

        return Option.None<PaymentError>();
    }
}
