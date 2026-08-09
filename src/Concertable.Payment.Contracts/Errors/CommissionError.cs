using Reunion.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record CommissionError : IError
{
    public ErrorDefinition Definition => this switch
    {
        BindingNotFound => ErrorDefinition.NotFound<BindingNotFound>("The commission binding was not found."),
        BindingMismatch => ErrorDefinition.Invalid<BindingMismatch>("The commission binding does not match this payment."),
        CurrencyMismatch => ErrorDefinition.Invalid<CurrencyMismatch>("The commission currency does not match this payment."),
        BindingIntentMismatch => ErrorDefinition.Invalid<BindingIntentMismatch>("The commission binding does not match the payment intent."),
        PricingChanged => ErrorDefinition.Conflict<PricingChanged>("The commission pricing has changed."),
        GrossNotConfirmed => ErrorDefinition.Conflict<GrossNotConfirmed>("The commission gross has not been confirmed."),
        GrossMismatch => ErrorDefinition.Conflict<GrossMismatch>("The commission gross does not match the confirmed amount.")
    };

    [ErrorCode("payment.commission_binding_not_found")]
    public partial record BindingNotFound;

    [ErrorCode("payment.commission_binding_mismatch")]
    public partial record BindingMismatch;

    [ErrorCode("payment.commission_currency_mismatch")]
    public partial record CurrencyMismatch;

    [ErrorCode("payment.commission_intent_mismatch")]
    public partial record BindingIntentMismatch;

    [ErrorCode("payment.commission_pricing_changed")]
    public partial record PricingChanged;

    [ErrorCode("payment.commission_gross_not_confirmed")]
    public partial record GrossNotConfirmed;

    [ErrorCode("payment.commission_gross_mismatch")]
    public partial record GrossMismatch;
}
