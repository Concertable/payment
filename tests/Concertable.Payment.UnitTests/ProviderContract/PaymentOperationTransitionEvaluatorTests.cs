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

    [Fact]
    public void Evaluate_UndefinedCurrentState_RejectsState()
    {
        var current = Attempt() with { State = (PaymentOperationState)999 };

        var result = PaymentOperationTransitionEvaluator.Evaluate(
            current,
            Observation(PaymentOperationState.Processing));

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidCurrentStateForProviderObject, rejection.Reason);
    }

    [Fact]
    public void Evaluate_UndefinedObservedState_RejectsState()
    {
        var result = PaymentOperationTransitionEvaluator.Evaluate(
            Attempt(),
            Observation((PaymentOperationState)999));

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind, rejection.Reason);
    }

    [Fact]
    public void Evaluate_UndefinedFailureClassification_RejectsFailure()
    {
        var observation = Observation(PaymentOperationState.RequiresPaymentMethod) with
        {
            FailureClassification = (ProviderFailureClassification)999
        };

        var result = PaymentOperationTransitionEvaluator.Evaluate(Attempt(), observation);

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification, rejection.Reason);
    }

    [Fact]
    public void Evaluate_FailureClassificationOnIncompatibleState_RejectsFailure()
    {
        var observation = Observation(PaymentOperationState.Processing) with
        {
            FailureClassification = ProviderFailureClassification.Declined
        };

        var result = PaymentOperationTransitionEvaluator.Evaluate(Attempt(), observation);

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification, rejection.Reason);
    }

    [Fact]
    public void Evaluate_DeclinedFailureClassification_MapsSafeFailure()
    {
        var observation = Observation(PaymentOperationState.RequiresPaymentMethod) with
        {
            FailureClassification = ProviderFailureClassification.Declined
        };

        var transition = EvaluateSuccess(Attempt(), observation);

        Assert.Equal(PaymentOperationFailureCode.Declined, transition.Failure?.Code);
        Assert.Equal("The payment was declined.", transition.Failure?.Message);
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
