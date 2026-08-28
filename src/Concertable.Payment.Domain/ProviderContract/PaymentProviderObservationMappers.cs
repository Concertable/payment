namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderObservationMappers
{
    extension(PaymentProviderObservation observation)
    {
        internal PaymentOperationTransition ToTransition() =>
            new(
                observation.State,
                observation.ProviderStatus,
                observation.ObservedAt,
                observation.CaptureBefore,
                observation.State.ToTerminalDisposition(observation.IsExplicitConsumerCancellation),
                observation.State.ToRetryDisposition(),
                observation.State.ToFailure(observation.FailureClassification));
    }
}
