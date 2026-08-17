using System.Collections.Frozen;

namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentOperationStateExtensions
{
    private static readonly FrozenSet<PaymentOperationState> terminalStates =
        new[]
        {
            PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled,
            PaymentOperationState.Failed
        }.ToFrozenSet();

    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationTerminalDisposition>
        terminalDispositions = new Dictionary<PaymentOperationState, PaymentOperationTerminalDisposition>
        {
            [PaymentOperationState.Creating] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.RequiresConfirmation] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.RequiresAction] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.Processing] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.Authorized] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.Succeeded] = PaymentOperationTerminalDisposition.OperationTerminal,
            [PaymentOperationState.Canceled] = PaymentOperationTerminalDisposition.AttemptTerminal,
            [PaymentOperationState.Failed] = PaymentOperationTerminalDisposition.AttemptTerminal
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationRetryDisposition>
        retryDispositions = new Dictionary<PaymentOperationState, PaymentOperationRetryDisposition>
        {
            [PaymentOperationState.Creating] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationRetryDisposition.RetryCurrentAttempt,
            [PaymentOperationState.RequiresConfirmation] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.RequiresAction] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.Processing] = PaymentOperationRetryDisposition.Reconcile,
            [PaymentOperationState.Authorized] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.Succeeded] = PaymentOperationRetryDisposition.NotRetryable,
            [PaymentOperationState.Canceled] = PaymentOperationRetryDisposition.NotRetryable,
            [PaymentOperationState.Failed] = PaymentOperationRetryDisposition.CreateNewAttempt
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationFailureCode> failures =
        new Dictionary<PaymentOperationState, PaymentOperationFailureCode>
        {
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationFailureCode.PaymentMethodRequired,
            [PaymentOperationState.RequiresAction] = PaymentOperationFailureCode.AuthenticationRequired,
            [PaymentOperationState.Canceled] = PaymentOperationFailureCode.Canceled,
            [PaymentOperationState.Failed] = PaymentOperationFailureCode.Unknown
        }.ToFrozenDictionary();

    extension(PaymentOperationState state)
    {
        internal bool IsTerminal() => terminalStates.Contains(state);

        internal PaymentOperationTerminalDisposition ToTerminalDisposition(bool isExplicitConsumerCancellation) =>
            state == PaymentOperationState.Canceled && isExplicitConsumerCancellation
                ? PaymentOperationTerminalDisposition.OperationTerminal
                : terminalDispositions[state];

        internal PaymentOperationRetryDisposition ToRetryDisposition() => retryDispositions[state];

        internal PaymentOperationFailure? ToFailure(PaymentOperationFailureCode? providerFailureCode)
        {
            if (providerFailureCode is { } code)
                return PaymentOperationFailure.FromCode(code);

            return failures.TryGetValue(state, out var defaultCode)
                ? PaymentOperationFailure.FromCode(defaultCode)
                : null;
        }
    }
}
