using System.Collections.Frozen;

namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderAttemptExtensions
{
    private static readonly FrozenDictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        intentTransitions = new Dictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        {
            [PaymentOperationState.Creating] = new[]
            {
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet(),
            [PaymentOperationState.RequiresPaymentMethod] = new[]
            {
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet(),
            [PaymentOperationState.RequiresConfirmation] = new[]
            {
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet(),
            [PaymentOperationState.RequiresAction] = new[]
            {
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet(),
            [PaymentOperationState.Processing] = new[]
            {
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet(),
            [PaymentOperationState.Authorized] = new[]
            {
                PaymentOperationState.Processing,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled
            }.ToFrozenSet()
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        refundTransitions = new Dictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        {
            [PaymentOperationState.Creating] = new[]
            {
                PaymentOperationState.Processing,
                PaymentOperationState.RequiresAction
            }.ToFrozenSet(),
            [PaymentOperationState.Processing] = new[]
            {
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet(),
            [PaymentOperationState.RequiresAction] = new[]
            {
                PaymentOperationState.Processing,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed
            }.ToFrozenSet()
        }.ToFrozenDictionary();

    extension(PaymentProviderAttempt current)
    {
        internal bool AllowsTransitionTo(PaymentOperationState next)
        {
            if (!current.Context.SupportsState(current.State)
                || !current.Context.SupportsState(next))
            {
                return false;
            }

            if (current.State == next)
                return true;

            var transitions = current.Context is PaymentProviderOperationContext.Refund
                ? refundTransitions
                : intentTransitions;

            return transitions.TryGetValue(current.State, out var allowed) && allowed.Contains(next);
        }
    }
}
