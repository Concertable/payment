using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.UnitTests.ProviderContract;

public sealed class PaymentOperationRetryAndExpiryEvaluatorTests
{
    private static readonly Guid operationId = Guid.Parse("019c1234-0000-7000-8000-000000000011");
    private static readonly Guid attemptId = Guid.Parse("019c1234-0000-7000-8000-000000000012");
    private static readonly Guid nextAttemptId = Guid.Parse("019c1234-0000-7000-8000-000000000013");
    private static readonly DateTimeOffset captureBefore = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NonConsumerRetriesNeverCreateARevision()
    {
        var cases = new[]
        {
            (PaymentOperationRetryTrigger.TransportRetry, PaymentOperationRetryDisposition.RetryCurrentAttempt),
            (PaymentOperationRetryTrigger.TimeoutRecovery, PaymentOperationRetryDisposition.RetryCurrentAttempt),
            (PaymentOperationRetryTrigger.WebhookRedelivery, PaymentOperationRetryDisposition.ContinueCurrentAttempt),
            (PaymentOperationRetryTrigger.Reconciliation, PaymentOperationRetryDisposition.Reconcile)
        };

        foreach (var (trigger, expected) in cases)
        {
            var decision = EvaluateRetrySuccess(
                Attempt(PaymentOperationState.RequiresAction),
                new PaymentOperationRetryRequest(trigger, "fingerprint-v1", nextAttemptId));

            Assert.Equal(expected, decision.Disposition);
            Assert.Equal(attemptId, decision.AttemptId);
            Assert.Equal(4, decision.Revision);
        }
    }

    [Theory]
    [InlineData(PaymentOperationState.Failed, PaymentOperationFailureCode.Declined)]
    [InlineData(PaymentOperationState.Canceled, PaymentOperationFailureCode.Expired)]
    public void ExplicitRetryOfEligibleAttemptCreatesTheNextRevision(
        PaymentOperationState state,
        PaymentOperationFailureCode failureCode)
    {
        var decision = EvaluateRetrySuccess(
            Attempt(state, new PaymentOperationFailure(failureCode, "Safe failure.")),
            new PaymentOperationRetryRequest(
                PaymentOperationRetryTrigger.ExplicitConsumerRetry,
                "fingerprint-v1",
                nextAttemptId));

        Assert.Equal(PaymentOperationRetryDisposition.CreateNewAttempt, decision.Disposition);
        Assert.Equal(nextAttemptId, decision.AttemptId);
        Assert.Equal(5, decision.Revision);
    }

    [Theory]
    [InlineData(PaymentOperationState.Creating)]
    [InlineData(PaymentOperationState.RequiresPaymentMethod)]
    [InlineData(PaymentOperationState.RequiresConfirmation)]
    [InlineData(PaymentOperationState.RequiresAction)]
    [InlineData(PaymentOperationState.Processing)]
    [InlineData(PaymentOperationState.Authorized)]
    [InlineData(PaymentOperationState.Succeeded)]
    [InlineData(PaymentOperationState.Canceled)]
    public void ExplicitRetryOfIneligibleAttemptIsNotRetryable(PaymentOperationState state)
    {
        var decision = EvaluateRetrySuccess(
            Attempt(state, new PaymentOperationFailure(PaymentOperationFailureCode.Canceled, "Canceled.")),
            new PaymentOperationRetryRequest(
                PaymentOperationRetryTrigger.ExplicitConsumerRetry,
                "fingerprint-v1",
                nextAttemptId));

        Assert.Equal(PaymentOperationRetryDisposition.NotRetryable, decision.Disposition);
        Assert.Equal(attemptId, decision.AttemptId);
        Assert.Equal(4, decision.Revision);
    }

    [Fact]
    public void ChangedFingerprintRejectsTheSameOperationIdentity()
    {
        var rejection = EvaluateRetryRejection(
            Attempt(PaymentOperationState.Failed),
            new PaymentOperationRetryRequest(
                PaymentOperationRetryTrigger.ExplicitConsumerRetry,
                "different-fingerprint",
                nextAttemptId));

        Assert.Equal(PaymentOperationTransitionRejectionReason.ImmutableBindingMismatch, rejection.Reason);
    }

    [Fact]
    public void UnknownRetryTriggerFailsClosed()
    {
        var rejection = EvaluateRetryRejection(
            Attempt(PaymentOperationState.Failed),
            new PaymentOperationRetryRequest(
                (PaymentOperationRetryTrigger)999,
                "fingerprint-v1",
                nextAttemptId));

        Assert.Equal(PaymentOperationTransitionRejectionReason.UnknownRetryTrigger, rejection.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("019c1234-0000-7000-8000-000000000012")]
    public void NewRevisionRequiresANewNonEmptyAttemptId(string? proposedAttemptId)
    {
        var rejection = EvaluateRetryRejection(
            Attempt(PaymentOperationState.Failed),
            new PaymentOperationRetryRequest(
                PaymentOperationRetryTrigger.ExplicitConsumerRetry,
                "fingerprint-v1",
                proposedAttemptId is null ? null : Guid.Parse(proposedAttemptId)));

        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidRetryAttempt, rejection.Reason);
    }

    [Fact]
    public void LocalClockBeforeCaptureDeadlineLeavesAuthorizationCurrent()
    {
        var decision = EvaluateExpirySuccess(
            AuthorizationAttempt(),
            captureBefore.AddTicks(-1),
            providerConfirmedUncaptured: false);

        Assert.Equal(PaymentAuthorizationExpiryDisposition.NotDue, decision.Disposition);
        Assert.Equal(PaymentOperationState.Authorized, decision.State);
        Assert.Equal(PaymentOperationRetryDisposition.ContinueCurrentAttempt, decision.RetryDisposition);
    }

    [Fact]
    public void LocalClockAtCaptureDeadlineRequiresProviderReconciliation()
    {
        var decision = EvaluateExpirySuccess(
            AuthorizationAttempt(),
            captureBefore,
            providerConfirmedUncaptured: false);

        Assert.Equal(PaymentAuthorizationExpiryDisposition.Reconcile, decision.Disposition);
        Assert.Equal(PaymentOperationState.Authorized, decision.State);
        Assert.Equal(PaymentOperationRetryDisposition.Reconcile, decision.RetryDisposition);
    }

    [Fact]
    public void ProviderConfirmedExpiryCancelsAttemptWithSafeExpiredFailure()
    {
        var decision = EvaluateExpirySuccess(
            AuthorizationAttempt(),
            captureBefore,
            providerConfirmedUncaptured: true);

        Assert.Equal(PaymentAuthorizationExpiryDisposition.Expired, decision.Disposition);
        Assert.Equal(PaymentOperationState.Canceled, decision.State);
        Assert.Equal(PaymentOperationTerminalDisposition.AttemptTerminal, decision.TerminalDisposition);
        Assert.Equal(PaymentOperationRetryDisposition.CreateNewAttempt, decision.RetryDisposition);
        Assert.Equal(PaymentOperationFailureCode.Expired, decision.Failure?.Code);
        Assert.Equal("The payment attempt expired.", decision.Failure?.Message);
    }

    [Theory]
    [InlineData(PaymentOperationState.Processing, PaymentSessionKind.Authorization, true)]
    [InlineData(PaymentOperationState.Authorized, PaymentSessionKind.Payment, true)]
    [InlineData(PaymentOperationState.Authorized, PaymentSessionKind.Authorization, false)]
    public void ExpiryRejectsInvalidAttemptEvidence(
        PaymentOperationState state,
        PaymentSessionKind sessionKind,
        bool hasCaptureBefore)
    {
        var current = Attempt(state) with
        {
            Context = OperationContext(sessionKind),
            CaptureBefore = hasCaptureBefore ? captureBefore : null
        };
        var result = PaymentAuthorizationExpiryEvaluator.Evaluate(
            current,
            captureBefore,
            providerConfirmedUncaptured: true);

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidAuthorizationExpiry, rejection.Reason);
    }

    private static PaymentProviderAttempt AuthorizationAttempt() =>
        Attempt(PaymentOperationState.Authorized) with
        {
            Context = new PaymentProviderOperationContext.Authorization(),
            CaptureBefore = captureBefore
        };

    private static PaymentProviderAttempt Attempt(
        PaymentOperationState state,
        PaymentOperationFailure? failure = null) =>
        new(
            operationId,
            attemptId,
            4,
            new PaymentProviderOperationContext.Payment(),
            "pi_test",
            state,
            "fingerprint-v1",
            Failure: failure);

    private static PaymentProviderOperationContext OperationContext(PaymentSessionKind sessionKind) =>
        sessionKind switch
        {
            PaymentSessionKind.Payment => new PaymentProviderOperationContext.Payment(),
            PaymentSessionKind.Authorization => new PaymentProviderOperationContext.Authorization(),
            _ => throw new ArgumentOutOfRangeException(nameof(sessionKind))
        };

    private static PaymentOperationRetryDecision EvaluateRetrySuccess(
        PaymentProviderAttempt current,
        PaymentOperationRetryRequest request)
    {
        var result = PaymentOperationRetryEvaluator.Evaluate(current, request);
        Assert.True(result.TryGetValue(out var decision));
        return decision;
    }

    private static PaymentOperationTransitionRejection EvaluateRetryRejection(
        PaymentProviderAttempt current,
        PaymentOperationRetryRequest request)
    {
        var result = PaymentOperationRetryEvaluator.Evaluate(current, request);
        Assert.True(result.TryGetError(out var rejection));
        return rejection;
    }

    private static PaymentAuthorizationExpiryDecision EvaluateExpirySuccess(
        PaymentProviderAttempt current,
        DateTimeOffset observedAt,
        bool providerConfirmedUncaptured)
    {
        var result = PaymentAuthorizationExpiryEvaluator.Evaluate(
            current,
            observedAt,
            providerConfirmedUncaptured);
        Assert.True(result.TryGetValue(out var decision));
        return decision;
    }

}
