using Concertable.Payment.Contracts;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Domain.Events;
using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentSessionAttemptEntityTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApplyTransition_ObservableStateChange_RaisesOneStateChangedEvent()
    {
        var attempt = BoundAttempt();

        attempt.ApplyTransition(PaymentSessionKind.Payment, Transition(PaymentOperationState.Processing, CreatedAt.AddSeconds(1)));

        var raised = Assert.Single(attempt.DomainEvents);
        var stateChanged = Assert.IsType<PaymentOperationStateChangedDomainEvent>(raised);
        Assert.Equal(PaymentOperationState.Processing, stateChanged.State);
        Assert.Equal(PaymentSessionKind.Payment, stateChanged.SessionKind);
        Assert.Equal(attempt.OperationId, stateChanged.Identity.OperationId);
        Assert.Equal(attempt.AttemptId, stateChanged.Identity.AttemptId);
        Assert.Equal(attempt.Revision, stateChanged.Identity.Revision);
    }

    [Fact]
    public void ApplyTransition_DuplicateDisposition_RaisesNoEvent()
    {
        var attempt = BoundAttempt();

        attempt.ApplyTransition(
            PaymentSessionKind.Payment,
            Transition(
                PaymentOperationState.Processing,
                CreatedAt.AddSeconds(1),
                disposition: PaymentOperationTransitionDisposition.Duplicate));

        Assert.Empty(attempt.DomainEvents);
    }

    [Fact]
    public void ApplyTransition_SameProjectionNewerObservation_RaisesNoSecondEvent()
    {
        var attempt = BoundAttempt();
        attempt.ApplyTransition(PaymentSessionKind.Payment, Transition(PaymentOperationState.Processing, CreatedAt.AddSeconds(1)));
        attempt.ClearDomainEvents();

        attempt.ApplyTransition(PaymentSessionKind.Payment, Transition(PaymentOperationState.Processing, CreatedAt.AddSeconds(2)));

        Assert.Empty(attempt.DomainEvents);
    }

    [Fact]
    public void ApplyTransition_FailureCodeChangesWithinSameState_RaisesEvent()
    {
        var attempt = BoundAttempt();
        attempt.ApplyTransition(
            PaymentSessionKind.Payment,
            Transition(
                PaymentOperationState.RequiresPaymentMethod,
                CreatedAt.AddSeconds(1),
                PaymentOperationFailure.FromCode(PaymentOperationFailureCode.PaymentMethodRequired)));
        attempt.ClearDomainEvents();

        attempt.ApplyTransition(
            PaymentSessionKind.Payment,
            Transition(
                PaymentOperationState.RequiresPaymentMethod,
                CreatedAt.AddSeconds(2),
                PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));

        var raised = Assert.Single(attempt.DomainEvents);
        var stateChanged = Assert.IsType<PaymentOperationStateChangedDomainEvent>(raised);
        Assert.Equal(PaymentOperationFailureCode.Declined, stateChanged.Failure?.Code);
    }

    private static PaymentSessionAttemptEntity BoundAttempt()
    {
        var attempt = PaymentSessionAttemptEntity.Create(
            Guid.CreateVersion7(CreatedAt),
            Guid.CreateVersion7(CreatedAt),
            1,
            null,
            PaymentSessionProviderObjectKind.PaymentIntent,
            CreatedAt);
        attempt.BindProviderObject("pi_test_attempt");
        return attempt;
    }

    private static PaymentOperationTransition Transition(
        PaymentOperationState state,
        DateTimeOffset observedAt,
        PaymentOperationFailure? failure = null,
        PaymentOperationTransitionDisposition disposition = PaymentOperationTransitionDisposition.Applied) =>
        new(
            disposition,
            state,
            state.ToString(),
            observedAt,
            null,
            PaymentOperationTerminalDisposition.NonTerminal,
            PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            failure);
}
