using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union]
public partial record CommissionError : IError
{
    partial record BindingNotFound;
    partial record BindingMismatch;
    partial record CurrencyMismatch;
    partial record BindingIntentMismatch;
    partial record PricingChanged;
    partial record ExpectedAmountsInvalid;

    public static CommissionError NotFound() => new BindingNotFound();
    public static CommissionError Mismatch() => new BindingMismatch();
    public static CommissionError InvalidCurrency() => new CurrencyMismatch();
    public static CommissionError IntentMismatch() => new BindingIntentMismatch();
    public static CommissionError ChangedPricing() => new PricingChanged();
    public static CommissionError InvalidExpectedAmounts() => new ExpectedAmountsInvalid();

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        bindingNotFound => ErrorDefinition.NotFound("payment.commission_binding_not_found", "The commission binding was not found."),
        bindingMismatch => ErrorDefinition.Invalid("payment.commission_binding_mismatch", "The commission binding does not match this payment."),
        currencyMismatch => ErrorDefinition.Invalid("payment.commission_currency_mismatch", "The commission currency does not match this payment."),
        bindingIntentMismatch => ErrorDefinition.Invalid("payment.commission_intent_mismatch", "The commission binding does not match the payment intent."),
        pricingChanged => ErrorDefinition.Conflict("payment.commission_pricing_changed", "The commission pricing has changed."),
        expectedAmountsInvalid => ErrorDefinition.Invalid("payment.commission_expected_amounts_invalid", "The expected commission amounts are invalid."));
}
