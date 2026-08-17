namespace Concertable.Payment.Domain.ProviderContract;

internal static class StripeOperationTransitionEvaluator
{
    public static Result<PaymentOperationTransition, PaymentOperationTransitionRejection> Evaluate(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
    {
        var normalized = observation.ToNormalized(current.State);
        if (!normalized.TryGetValue(out var observed))
        {
            if (normalized.TryGetError(out var rejection))
                return rejection;

            throw new InvalidOperationException("The provider normalization result was uninitialized.");
        }

        return PaymentOperationTransitionEvaluator.Evaluate(current, observed);
    }
}
