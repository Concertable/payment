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
            [StripeProviderObjectKind.PaymentIntent] =
                new Dictionary<string, PaymentOperationState>(StringComparer.Ordinal)
                {
                    ["requires_payment_method"] = PaymentOperationState.RequiresPaymentMethod,
                    ["requires_confirmation"] = PaymentOperationState.RequiresConfirmation,
                    ["requires_action"] = PaymentOperationState.RequiresAction,
                    ["processing"] = PaymentOperationState.Processing,
                    ["requires_capture"] = PaymentOperationState.Authorized,
                    ["canceled"] = PaymentOperationState.Canceled,
                    ["succeeded"] = PaymentOperationState.Succeeded
                }.ToFrozenDictionary(StringComparer.Ordinal),
            [StripeProviderObjectKind.SetupIntent] =
                new Dictionary<string, PaymentOperationState>(StringComparer.Ordinal)
                {
                    ["requires_payment_method"] = PaymentOperationState.RequiresPaymentMethod,
                    ["requires_confirmation"] = PaymentOperationState.RequiresConfirmation,
                    ["requires_action"] = PaymentOperationState.RequiresAction,
                    ["processing"] = PaymentOperationState.Processing,
                    ["canceled"] = PaymentOperationState.Canceled,
                    ["succeeded"] = PaymentOperationState.Succeeded
                }.ToFrozenDictionary(StringComparer.Ordinal),
            [StripeProviderObjectKind.Refund] =
                new Dictionary<string, PaymentOperationState>(StringComparer.Ordinal)
                {
                    ["pending"] = PaymentOperationState.Processing,
                    ["requires_action"] = PaymentOperationState.RequiresAction,
                    ["succeeded"] = PaymentOperationState.Succeeded,
                    ["failed"] = PaymentOperationState.Failed,
                    ["canceled"] = PaymentOperationState.Canceled
                }.ToFrozenDictionary(StringComparer.Ordinal)
        }.ToFrozenDictionary();
}
