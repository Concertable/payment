using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.UnitTests.ProviderContract;

public sealed class PaymentOperationTransitionEvaluatorTests
{
    private static readonly Guid operationId = Guid.Parse("019c1234-0000-7000-8000-000000000021");
    private static readonly Guid attemptId = Guid.Parse("019c1234-0000-7000-8000-000000000022");
    private static readonly DateTimeOffset observedAt = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ProviderNeutralObservation_AppliesTransition()
    {
        var current = Attempt();
        var observation = Observation(PaymentOperationState.Processing);

        var transition = EvaluateSuccess(current, observation);

        Assert.Equal(PaymentOperationTransitionDisposition.Applied, transition.Disposition);
        Assert.Equal(PaymentOperationState.Processing, transition.State);
        Assert.Equal(PaymentOperationRetryDisposition.Reconcile, transition.RetryDisposition);
    }

    [Fact]
    public void Evaluate_UnchangedPersistedProjection_ReturnsDuplicate()
    {
        var current = Attempt() with
        {
            State = PaymentOperationState.Processing,
            LastProviderStatus = "processing",
            LastObservedAt = observedAt
        };
        var observation = Observation(PaymentOperationState.Processing);

        var transition = EvaluateSuccess(current, observation);

        Assert.Equal(PaymentOperationTransitionDisposition.Duplicate, transition.Disposition);
    }

    [Fact]
    public void Evaluate_DifferentProviderProduct_RejectsBinding()
    {
        var current = Attempt();
        var observation = Observation(PaymentOperationState.Processing) with
        {
            Context = new PaymentProviderOperationContext.Refund()
        };

        var result = PaymentOperationTransitionEvaluator.Evaluate(current, observation);

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.ProviderObjectMismatch, rejection.Reason);
    }

    private static PaymentProviderAttempt Attempt() =>
        new(
            operationId,
            attemptId,
            1,
            new PaymentProviderOperationContext.Payment(),
            "provider-payment-1",
            PaymentOperationState.Creating,
            "fingerprint-v1");

    private static PaymentProviderObservation Observation(PaymentOperationState state) =>
        new(
            new PaymentProviderOperationContext.Payment(),
            "provider-payment-1",
            operationId,
            attemptId,
            1,
            state,
            "processing",
            observedAt,
            null,
            null,
            false);

    private static PaymentOperationTransition EvaluateSuccess(
        PaymentProviderAttempt current,
        PaymentProviderObservation observation)
    {
        var result = PaymentOperationTransitionEvaluator.Evaluate(current, observation);
        Assert.True(result.TryGetValue(out var transition));
        return transition;
    }
}
