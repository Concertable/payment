using Concertable.Kernel;
using Concertable.Payment.Domain.ProviderContract;
using static Concertable.Payment.Domain.Lifecycle.PaymentOperationTrigger;

namespace Concertable.Payment.Domain.Lifecycle;

internal sealed class PaymentSessionStateMachine : StateMachine<PaymentOperationState, PaymentOperationTrigger>
{
    public PaymentSessionStateMachine()
        : base(
        [
            (PaymentOperationState.RequiresPaymentMethod, RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.RequiresConfirmation, RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.RequiresAction, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Processing, Process, PaymentOperationState.Processing),
            (PaymentOperationState.Authorized, Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.Succeeded, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Canceled, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Failed, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.Creating, RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.Creating, RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.Creating, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Creating, Process, PaymentOperationState.Processing),
            (PaymentOperationState.Creating, Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.Creating, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Creating, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Creating, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresPaymentMethod, RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.RequiresPaymentMethod, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.RequiresPaymentMethod, Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresPaymentMethod, Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.RequiresPaymentMethod, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresPaymentMethod, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresPaymentMethod, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresConfirmation, RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.RequiresConfirmation, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.RequiresConfirmation, Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresConfirmation, Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.RequiresConfirmation, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresConfirmation, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresConfirmation, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresAction, RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.RequiresAction, RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.RequiresAction, Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresAction, Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.RequiresAction, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresAction, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresAction, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.Processing, RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.Processing, RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Processing, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Processing, Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Processing, Fail, PaymentOperationState.Failed),

            (PaymentOperationState.Authorized, Process, PaymentOperationState.Processing),
            (PaymentOperationState.Authorized, Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Authorized, Cancel, PaymentOperationState.Canceled)
        ])
    {
    }

    internal Result<PaymentOperationTransition, PaymentOperationTransitionRejection> Evaluate(
        PaymentOperationState currentState,
        PaymentProviderObservation observation)
    {
        if (currentState.IsTerminal() && observation.State != currentState)
            return Reject(currentState, PaymentOperationTransitionRejectionReason.TerminalStateProtected, observation.State);

        if (Transition(currentState, observation.State.ToTrigger()).TryGetError(out _))
            return Reject(currentState, PaymentOperationTransitionRejectionReason.IllegalTransition, observation.State);

        if (observation.State == PaymentOperationState.Authorized)
        {
            if (observation.Context is PaymentProviderOperationContext.Payment)
                return Reject(currentState, PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind, observation.State);
        }

        return observation.ToTransition();
    }

    private static PaymentOperationTransitionRejection Reject(
        PaymentOperationState currentState,
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState observedState) =>
        new(reason, currentState, observedState);
}
