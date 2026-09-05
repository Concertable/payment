using System.Net;
using Concertable.Payment.Application.Errors;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeFailureClassifier
{
    private const string AuthenticationRequiredDeclineCode = "authentication_required";

    public static Option<ChargeError> Classify(StripeException exception)
    {
        if (exception.HttpStatusCode != HttpStatusCode.PaymentRequired &&
            !string.Equals(exception.StripeError?.Type, "card_error", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(exception.StripeError?.DeclineCode))
            return null;

        ChargeError rejection = string.Equals(
            exception.StripeError?.DeclineCode,
            AuthenticationRequiredDeclineCode,
            StringComparison.Ordinal)
            ? new ChargeError.AuthenticationRequired()
            : new ChargeError.PaymentFailure(new PaymentError.PaymentRejected());

        return rejection;
    }
}
