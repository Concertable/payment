using System.Net;
using Concertable.Payment.Application.Errors;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeFailureClassifier
{
    private const string AuthenticationRequiredDeclineCode = "authentication_required";

    public static Option<PaymentRejection> Classify(StripeException exception)
    {
        if (exception.HttpStatusCode != HttpStatusCode.PaymentRequired &&
            !string.Equals(exception.StripeError?.Type, "card_error", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(exception.StripeError?.DeclineCode))
            return Option.None<PaymentRejection>();

        var recovery = string.Equals(
            exception.StripeError?.DeclineCode,
            AuthenticationRequiredDeclineCode,
            StringComparison.Ordinal)
            ? PaymentRecovery.OnSessionAuthentication
            : PaymentRecovery.NewPaymentMethod;

        return Option.Some(new PaymentRejection
        {
            Error = new PaymentError.PaymentRejected(),
            Recovery = recovery
        });
    }
}
