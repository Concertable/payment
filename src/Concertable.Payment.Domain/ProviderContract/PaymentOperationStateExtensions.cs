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

    extension(PaymentOperationState state)
    {
        internal bool IsTerminal() => terminalStates.Contains(state);
    }
}
