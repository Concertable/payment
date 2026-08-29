using Concertable.Payment.Contracts;
using Concertable.Payment.Domain.Lifecycle;
using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.UnitTests.Lifecycle;

public sealed class PaymentSessionStateMachineTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    private readonly PaymentSessionStateMachine machine = new();

    [Theory]
    [InlineData(PaymentOperationState.Creating, PaymentOperationState.RequiresConfirmation)]
    [InlineData(PaymentOperationState.RequiresConfirmation, PaymentOperationState.Processing)]
    [InlineData(PaymentOperationState.Processing, PaymentOperationState.Succeeded)]
    [InlineData(PaymentOperationState.Authorized, PaymentOperationState.Succeeded)]
    public void Evaluate_LegalEdge_AppliesObservedState(
        PaymentOperationState current,
        PaymentOperationState observed)
    {
        var result = machine.Evaluate(current, Observation(observed));

        Assert.True(result.TryGetValue(out var transition));
        Assert.Equal(observed, transition.State);
    }

    [Fact]
    public void Evaluate_AuthorizedWithCaptureDeadline_IsApplied()
    {
        var result = machine.Evaluate(
            PaymentOperationState.Creating,
            Observation(
                PaymentOperationState.Authorized,
                captureBefore: ObservedAt.AddDays(7),
                context: new PaymentProviderOperationContext.Authorization()));

        Assert.True(result.TryGetValue(out var transition));
        Assert.Equal(PaymentOperationState.Authorized, transition.State);
    }

    [Fact]
    public void Evaluate_AuthorizedForAutomaticPayment_IsRejected()
    {
        var result = machine.Evaluate(
            PaymentOperationState.Creating,
            Observation(PaymentOperationState.Authorized, captureBefore: ObservedAt.AddDays(7)));

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind, rejection.Reason);
    }

    [Fact]
    public void Evaluate_IllegalEdge_IsRejected()
    {
        var result = machine.Evaluate(PaymentOperationState.Processing, Observation(PaymentOperationState.RequiresConfirmation));

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.IllegalTransition, rejection.Reason);
    }

    [Fact]
    public void Evaluate_TerminalRegression_IsProtected()
    {
        var result = machine.Evaluate(PaymentOperationState.Succeeded, Observation(PaymentOperationState.Processing));

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.TerminalStateProtected, rejection.Reason);
    }

    [Fact]
    public void Evaluate_TerminalReObservedInSameState_IsAppliedNoOp()
    {
        var result = machine.Evaluate(PaymentOperationState.Succeeded, Observation(PaymentOperationState.Succeeded));

        Assert.True(result.TryGetValue(out var transition));
        Assert.Equal(PaymentOperationState.Succeeded, transition.State);
    }

    [Fact]
    public void Evaluate_AuthorizedWithoutCaptureDeadline_IsRejected()
    {
        var result = machine.Evaluate(
            PaymentOperationState.Creating,
            Observation(
                PaymentOperationState.Authorized,
                captureBefore: null,
                context: new PaymentProviderOperationContext.Authorization()));

        Assert.True(result.TryGetError(out var rejection));
        Assert.Equal(PaymentOperationTransitionRejectionReason.CaptureDeadlineRequired, rejection.Reason);
    }

    [Fact]
    public void Evaluate_ExplicitConsumerCancellation_TerminatesTheOperation()
    {
        var result = machine.Evaluate(
            PaymentOperationState.Creating,
            Observation(PaymentOperationState.Canceled) with { IsExplicitConsumerCancellation = true });

        Assert.True(result.TryGetValue(out var transition));
        Assert.Equal(PaymentOperationTerminalDisposition.OperationTerminal, transition.TerminalDisposition);
        Assert.Equal(PaymentOperationRetryDisposition.NotRetryable, transition.RetryDisposition);
    }

    [Fact]
    public void Evaluate_ProviderCancellation_TerminatesOnlyTheAttempt()
    {
        var result = machine.Evaluate(PaymentOperationState.Creating, Observation(PaymentOperationState.Canceled));

        Assert.True(result.TryGetValue(out var transition));
        Assert.Equal(PaymentOperationTerminalDisposition.AttemptTerminal, transition.TerminalDisposition);
    }

    [Fact]
    public void Evaluate_ClassifiedDecline_KeepsRecoverableStateAndSafeFailure()
    {
        var result = machine.Evaluate(
            PaymentOperationState.Creating,
            Observation(PaymentOperationState.RequiresPaymentMethod) with
            {
                FailureClassification = ProviderFailureClassification.Declined
            });

        Assert.True(result.TryGetValue(out var transition));
        Assert.Equal(PaymentOperationState.RequiresPaymentMethod, transition.State);
        Assert.Equal(PaymentOperationRetryDisposition.RetryCurrentAttempt, transition.RetryDisposition);
        Assert.Equal(PaymentOperationFailureCode.Declined, transition.Failure?.Code);
        Assert.Equal("The payment was declined.", transition.Failure?.Message);
    }

    [Fact]
    public void SessionEdges_MatchTheDeclaredGraph()
    {
        AssertEdges(machine, SessionEdges);
    }

    private static void AssertEdges(
        Concertable.Kernel.IStateMachine<PaymentOperationState, PaymentOperationTrigger> subject,
        HashSet<(PaymentOperationState From, PaymentOperationState To)> expected)
    {
        foreach (var from in Enum.GetValues<PaymentOperationState>())
            foreach (var to in Enum.GetValues<PaymentOperationState>())
            {
                if (to == PaymentOperationState.Creating)
                    continue;

                var legal = subject.Transition(from, to.ToTrigger()).TryGetValue(out _);
                Assert.Equal(expected.Contains((from, to)), legal);
            }
    }

    private static readonly HashSet<(PaymentOperationState, PaymentOperationState)> SessionEdges =
    [
        .. Edges(PaymentOperationState.RequiresPaymentMethod, PaymentOperationState.RequiresPaymentMethod),
        .. Edges(PaymentOperationState.RequiresConfirmation, PaymentOperationState.RequiresConfirmation),
        .. Edges(PaymentOperationState.RequiresAction, PaymentOperationState.RequiresAction),
        .. Edges(PaymentOperationState.Processing, PaymentOperationState.Processing),
        .. Edges(PaymentOperationState.Authorized, PaymentOperationState.Authorized),
        .. Edges(PaymentOperationState.Succeeded, PaymentOperationState.Succeeded),
        .. Edges(PaymentOperationState.Canceled, PaymentOperationState.Canceled),
        .. Edges(PaymentOperationState.Failed, PaymentOperationState.Failed),
        .. Edges(PaymentOperationState.Creating,
            PaymentOperationState.RequiresPaymentMethod, PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.RequiresAction, PaymentOperationState.Processing, PaymentOperationState.Authorized,
            PaymentOperationState.Succeeded, PaymentOperationState.Canceled, PaymentOperationState.Failed),
        .. Edges(PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresConfirmation, PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing, PaymentOperationState.Authorized, PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled, PaymentOperationState.Failed),
        .. Edges(PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.RequiresPaymentMethod, PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing, PaymentOperationState.Authorized, PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled, PaymentOperationState.Failed),
        .. Edges(PaymentOperationState.RequiresAction,
            PaymentOperationState.RequiresPaymentMethod, PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.Processing, PaymentOperationState.Authorized, PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled, PaymentOperationState.Failed),
        .. Edges(PaymentOperationState.Processing,
            PaymentOperationState.RequiresPaymentMethod, PaymentOperationState.RequiresAction,
            PaymentOperationState.Succeeded, PaymentOperationState.Canceled, PaymentOperationState.Failed),
        .. Edges(PaymentOperationState.Authorized,
            PaymentOperationState.Processing, PaymentOperationState.Succeeded, PaymentOperationState.Canceled)
    ];

    private static IEnumerable<(PaymentOperationState, PaymentOperationState)> Edges(
        PaymentOperationState from,
        params PaymentOperationState[] targets) =>
        targets.Select(to => (from, to));

    private static PaymentProviderObservation Observation(
        PaymentOperationState state,
        DateTimeOffset? captureBefore = null,
        PaymentProviderOperationContext? context = null) =>
        new(
            context ?? new PaymentProviderOperationContext.Payment(),
            "pi_test",
            Guid.Parse("019c1234-0000-7000-8000-000000000001"),
            Guid.Parse("019c1234-0000-7000-8000-000000000002"),
            1,
            state,
            state.ToString(),
            ObservedAt,
            captureBefore,
            null,
            false);
}
