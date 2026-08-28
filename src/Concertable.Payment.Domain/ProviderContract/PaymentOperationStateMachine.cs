using System.Collections.Frozen;
using Concertable.Kernel;

namespace Concertable.Payment.Domain.ProviderContract;

internal enum PaymentOperationTrigger
{
    RequirePaymentMethod,
    RequireConfirmation,
    RequireAction,
    Process,
    Authorize,
    Succeed,
    Cancel,
    Fail
}

internal static class PaymentOperationStateMachine
{
    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationTrigger> triggers =
        new Dictionary<PaymentOperationState, PaymentOperationTrigger>
        {
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationTrigger.RequirePaymentMethod,
            [PaymentOperationState.RequiresConfirmation] = PaymentOperationTrigger.RequireConfirmation,
            [PaymentOperationState.RequiresAction] = PaymentOperationTrigger.RequireAction,
            [PaymentOperationState.Processing] = PaymentOperationTrigger.Process,
            [PaymentOperationState.Authorized] = PaymentOperationTrigger.Authorize,
            [PaymentOperationState.Succeeded] = PaymentOperationTrigger.Succeed,
            [PaymentOperationState.Canceled] = PaymentOperationTrigger.Cancel,
            [PaymentOperationState.Failed] = PaymentOperationTrigger.Fail
        }.ToFrozenDictionary();

    private static readonly IStateMachine<PaymentOperationState, PaymentOperationTrigger> intentMachine =
        new StateMachine<PaymentOperationState, PaymentOperationTrigger>(
        [
            (PaymentOperationState.Creating, PaymentOperationTrigger.RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.Creating, PaymentOperationTrigger.RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.Creating, PaymentOperationTrigger.RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Creating, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.Creating, PaymentOperationTrigger.Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.Creating, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Creating, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Creating, PaymentOperationTrigger.Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresPaymentMethod, PaymentOperationTrigger.Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresConfirmation, PaymentOperationTrigger.Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.RequireConfirmation, PaymentOperationState.RequiresConfirmation),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Authorize, PaymentOperationState.Authorized),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Fail, PaymentOperationState.Failed),

            (PaymentOperationState.Processing, PaymentOperationTrigger.RequirePaymentMethod, PaymentOperationState.RequiresPaymentMethod),
            (PaymentOperationState.Processing, PaymentOperationTrigger.RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Processing, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Processing, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Processing, PaymentOperationTrigger.Fail, PaymentOperationState.Failed),

            (PaymentOperationState.Authorized, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.Authorized, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Authorized, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled)
        ]);

    private static readonly IStateMachine<PaymentOperationState, PaymentOperationTrigger> refundMachine =
        new StateMachine<PaymentOperationState, PaymentOperationTrigger>(
        [
            (PaymentOperationState.Creating, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.Creating, PaymentOperationTrigger.RequireAction, PaymentOperationState.RequiresAction),

            (PaymentOperationState.Processing, PaymentOperationTrigger.RequireAction, PaymentOperationState.RequiresAction),
            (PaymentOperationState.Processing, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.Processing, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.Processing, PaymentOperationTrigger.Fail, PaymentOperationState.Failed),

            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Process, PaymentOperationState.Processing),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Succeed, PaymentOperationState.Succeeded),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Cancel, PaymentOperationState.Canceled),
            (PaymentOperationState.RequiresAction, PaymentOperationTrigger.Fail, PaymentOperationState.Failed)
        ]);

    internal static bool Allows(
        PaymentProviderOperationContext context,
        PaymentOperationState from,
        PaymentOperationState to)
    {
        if (!context.SupportsState(from) || !context.SupportsState(to))
            return false;
        if (from == to)
            return true;
        if (!triggers.TryGetValue(to, out var trigger))
            return false;

        var machine = context is PaymentProviderOperationContext.Refund ? refundMachine : intentMachine;
        return machine.Transition(from, trigger).TryGetValue(out _);
    }
}
