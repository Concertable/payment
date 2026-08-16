using System.Collections.Frozen;

namespace Concertable.Payment.Domain.ProviderContract;

internal enum StripeProviderObjectKind
{
    PaymentIntent,
    SetupIntent,
    Refund
}

internal static class StripeProviderContractBaseline
{
    public const string StripeNetVersion = "47.3.0";
    public const string ApiVersion = "2025-01-27.acacia";

    public static readonly FrozenDictionary<StripeProviderObjectKind, FrozenDictionary<string, PaymentOperationState>>
        NormalizedStates = new Dictionary<StripeProviderObjectKind, FrozenDictionary<string, PaymentOperationState>>
        {
            [StripeProviderObjectKind.PaymentIntent] = Freeze(
                ("requires_payment_method", PaymentOperationState.RequiresPaymentMethod),
                ("requires_confirmation", PaymentOperationState.RequiresConfirmation),
                ("requires_action", PaymentOperationState.RequiresAction),
                ("processing", PaymentOperationState.Processing),
                ("requires_capture", PaymentOperationState.Authorized),
                ("canceled", PaymentOperationState.Canceled),
                ("succeeded", PaymentOperationState.Succeeded)),
            [StripeProviderObjectKind.SetupIntent] = Freeze(
                ("requires_payment_method", PaymentOperationState.RequiresPaymentMethod),
                ("requires_confirmation", PaymentOperationState.RequiresConfirmation),
                ("requires_action", PaymentOperationState.RequiresAction),
                ("processing", PaymentOperationState.Processing),
                ("canceled", PaymentOperationState.Canceled),
                ("succeeded", PaymentOperationState.Succeeded)),
            [StripeProviderObjectKind.Refund] = Freeze(
                ("pending", PaymentOperationState.Processing),
                ("requires_action", PaymentOperationState.RequiresAction),
                ("succeeded", PaymentOperationState.Succeeded),
                ("failed", PaymentOperationState.Failed),
                ("canceled", PaymentOperationState.Canceled))
        }.ToFrozenDictionary();

    private static FrozenDictionary<string, PaymentOperationState> Freeze(
        params (string Status, PaymentOperationState State)[] states) =>
        states.ToFrozenDictionary(entry => entry.Status, entry => entry.State, StringComparer.Ordinal);
}
