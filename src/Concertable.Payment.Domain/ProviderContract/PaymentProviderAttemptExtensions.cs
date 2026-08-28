namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderAttemptExtensions
{
    extension(PaymentProviderAttempt current)
    {
        internal bool AllowsTransitionTo(PaymentOperationState next) =>
            PaymentOperationStateMachine.Allows(current.Context, current.State, next);
    }
}
