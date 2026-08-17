namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderObservationMappers
{
    extension(PaymentProviderAttempt current)
    {
        internal PaymentOperationPersistedProjection ToPersistedProjection() =>
            new(
                current.State,
                current.LastProviderStatus,
                current.LastObservedAt,
                current.CaptureBefore,
                current.Failure);
    }

    extension(PaymentProviderObservation observation)
    {
        internal PaymentOperationPersistedProjection ToPersistedProjection() =>
            new(
                observation.State,
                observation.ProviderStatus,
                observation.ObservedAt,
                observation.CaptureBefore,
                observation.State.ToFailure(observation.FailureCode));

        internal PaymentOperationTransition ToTransition(PaymentOperationTransitionDisposition disposition) =>
            new(
                disposition,
                observation.State,
                observation.ProviderStatus,
                observation.ObservedAt,
                observation.CaptureBefore,
                observation.State.ToTerminalDisposition(observation.IsExplicitConsumerCancellation),
                observation.State.ToRetryDisposition(),
                observation.State.ToFailure(observation.FailureCode));
    }
}
