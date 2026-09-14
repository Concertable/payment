using System.Collections.Frozen;

namespace Concertable.Payment.Domain.Lifecycle;

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

internal static class PaymentOperationTriggers
{
    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationTrigger> byObservedState =
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

    extension(PaymentOperationState observedState)
    {
        internal PaymentOperationTrigger ToTrigger() =>
            byObservedState.TryGetValue(observedState, out var trigger)
                ? trigger
                : throw new DomainException($"Payment operation state {observedState} is not an observable provider outcome.");
    }
}
