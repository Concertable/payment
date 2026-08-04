using Concertable.Kernel.Errors;
namespace Concertable.Payment.Contracts.Errors;

public sealed record CommissionError(ErrorDefinition Definition) : IError
{
    public static readonly CommissionError BindingNotFound = new(
        ErrorDefinition.NotFound("payment.commission_binding_not_found", "The commission binding was not found."));

    public static readonly CommissionError BindingMismatch = new(
        ErrorDefinition.Invalid("payment.commission_binding_mismatch", "The commission binding does not match this payment."));

    public static readonly CommissionError CurrencyMismatch = new(
        ErrorDefinition.Invalid("payment.commission_currency_mismatch", "The commission currency does not match this payment."));

    public static readonly CommissionError BindingIntentMismatch = new(
        ErrorDefinition.Invalid("payment.commission_intent_mismatch", "The commission binding does not match the payment intent."));

    public static readonly CommissionError PricingChanged = new(
        ErrorDefinition.Conflict("payment.commission_pricing_changed", "The commission pricing has changed."));

    public static readonly CommissionError ExpectedAmountsInvalid = new(
        ErrorDefinition.Invalid("payment.commission_expected_amounts_invalid", "The expected commission amounts are invalid."));
}
