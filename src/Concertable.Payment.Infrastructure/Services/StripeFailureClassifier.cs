using System.Net;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeFailureClassifier
{
    public static Option<PaymentError> Classify(StripeException exception)
    {
        if (exception.HttpStatusCode == HttpStatusCode.PaymentRequired ||
            string.Equals(exception.StripeError?.Type, "card_error", StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(exception.StripeError?.DeclineCode))
            return Option.Some<PaymentError>(new PaymentError.PaymentRejected());

        return Option.None<PaymentError>();
    }
}
