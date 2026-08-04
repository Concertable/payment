using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public partial record PaymentError : IError
{
    public partial record PayerNotFoundCase;
    public partial record PayeeNotFoundCase;
    public partial record PayerNotConfiguredCase;
    public partial record PayeeNotConfiguredCase;
    public partial record PayeePayoutsUnavailableCase;
    public partial record DeclinedCase;
    public partial record RejectedCase;

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        _ => ErrorDefinition.NotFound("payment.payer_not_found", "Payer payment account not found."),
        _ => ErrorDefinition.NotFound("payment.payee_not_found", "Payee payment account not found."),
        _ => ErrorDefinition.Invalid("payment.payer_not_configured", "Payer payment account is not configured."),
        _ => ErrorDefinition.Invalid("payment.payee_not_configured", "Payee payment account is not configured."),
        _ => ErrorDefinition.Invalid("payment.payee_payouts_unavailable", "Payee is not eligible for payouts."),
        _ => ErrorDefinition.PaymentRequired("payment.declined", "The payment was declined."),
        _ => ErrorDefinition.PaymentRequired("payment.rejected", "The payment provider rejected the operation."));

    public static PaymentError PayerNotFound() => new PayerNotFoundCase();
    public static PaymentError PayeeNotFound() => new PayeeNotFoundCase();
    public static PaymentError PayerNotConfigured() => new PayerNotConfiguredCase();
    public static PaymentError PayeeNotConfigured() => new PayeeNotConfiguredCase();
    public static PaymentError PayeePayoutsUnavailable() => new PayeePayoutsUnavailableCase();
    public static PaymentError Declined() => new DeclinedCase();
    public static PaymentError Rejected() => new RejectedCase();

    public static Option<PaymentError> FromCode(string code) => code switch
    {
        "payment.payer_not_found" => Option.Some(PayerNotFound()),
        "payment.payee_not_found" => Option.Some(PayeeNotFound()),
        "payment.payer_not_configured" => Option.Some(PayerNotConfigured()),
        "payment.payee_not_configured" => Option.Some(PayeeNotConfigured()),
        "payment.payee_payouts_unavailable" => Option.Some(PayeePayoutsUnavailable()),
        "payment.declined" => Option.Some(Declined()),
        "payment.rejected" => Option.Some(Rejected()),
        _ => Option.None<PaymentError>()
    };
}

[Union(EnableImplicitConversions = false)]
public partial record CommissionError : IError
{
    public partial record CurrencyMismatchCase;
    public partial record PricingChangedCase;
    public partial record BindingNotFoundCase;
    public partial record BindingMismatchCase;
    public partial record BindingIntentMismatchCase;
    public partial record ExpectedAmountsInvalidCase;

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        _ => ErrorDefinition.Invalid("commission.currency_mismatch", "Commission currency does not match."),
        _ => ErrorDefinition.Conflict("commission.pricing_changed", "Commission pricing has changed."),
        _ => ErrorDefinition.NotFound("commission.binding_not_found", "Commission binding not found."),
        _ => ErrorDefinition.Conflict("commission.binding_mismatch", "Commission binding does not match the operation."),
        _ => ErrorDefinition.Conflict("commission.binding_intent_mismatch", "Commission binding does not match the payment intent."),
        _ => ErrorDefinition.Invalid("commission.expected_amounts_invalid", "Expected commission amounts are invalid."));

    public static CommissionError CurrencyMismatch() => new CurrencyMismatchCase();
    public static CommissionError PricingChanged() => new PricingChangedCase();
    public static CommissionError BindingNotFound() => new BindingNotFoundCase();
    public static CommissionError BindingMismatch() => new BindingMismatchCase();
    public static CommissionError BindingIntentMismatch() => new BindingIntentMismatchCase();
    public static CommissionError ExpectedAmountsInvalid() => new ExpectedAmountsInvalidCase();

    public static Option<CommissionError> FromCode(string code) => code switch
    {
        "commission.currency_mismatch" => Option.Some(CurrencyMismatch()),
        "commission.pricing_changed" => Option.Some(PricingChanged()),
        "commission.binding_not_found" => Option.Some(BindingNotFound()),
        "commission.binding_mismatch" => Option.Some(BindingMismatch()),
        "commission.binding_intent_mismatch" => Option.Some(BindingIntentMismatch()),
        "commission.expected_amounts_invalid" => Option.Some(ExpectedAmountsInvalid()),
        _ => Option.None<CommissionError>()
    };
}

[Union(EnableImplicitConversions = false)]
public partial record ManagerPaymentError : IError
{
    public partial record PaymentCase(PaymentError Error);
    public partial record CommissionCase(CommissionError Error);

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        payment => payment.Error.Definition,
        commission => commission.Error.Definition);

    public static ManagerPaymentError Payment(PaymentError error) => new PaymentCase(error);
    public static ManagerPaymentError Commission(CommissionError error) => new CommissionCase(error);

    public static Option<ManagerPaymentError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some(Payment(payment)),
            () => CommissionError.FromCode(code).Map(Commission));
}

[Union(EnableImplicitConversions = false)]
public partial record EscrowDepositError : IError
{
    public partial record PaymentCase(PaymentError Error);
    public partial record CommissionCase(CommissionError Error);

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        payment => payment.Error.Definition,
        commission => commission.Error.Definition);

    public static EscrowDepositError Payment(PaymentError error) => new PaymentCase(error);
    public static EscrowDepositError Commission(CommissionError error) => new CommissionCase(error);

    public static Option<EscrowDepositError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some(Payment(payment)),
            () => CommissionError.FromCode(code).Map(Commission));
}

[Union(EnableImplicitConversions = false)]
public partial record EscrowCaptureError : IError
{
    public partial record PaymentCase(PaymentError Error);
    public partial record CommissionCase(CommissionError Error);

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        payment => payment.Error.Definition,
        commission => commission.Error.Definition);

    public static EscrowCaptureError Payment(PaymentError error) => new PaymentCase(error);
    public static EscrowCaptureError Commission(CommissionError error) => new CommissionCase(error);

    public static Option<EscrowCaptureError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some(Payment(payment)),
            () => CommissionError.FromCode(code).Map(Commission));
}

[Union(EnableImplicitConversions = false)]
public partial record EscrowReleaseError : IError
{
    public partial record EscrowNotFoundCase;
    public partial record EscrowNotHeldCase;
    public partial record PaymentCase(PaymentError Error);

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        _ => ErrorDefinition.NotFound("escrow.release_not_found", "Escrow not found."),
        _ => ErrorDefinition.Conflict("escrow.release_not_held", "Only held escrow can be released."),
        payment => payment.Error.Definition);

    public static EscrowReleaseError EscrowNotFound() => new EscrowNotFoundCase();
    public static EscrowReleaseError EscrowNotHeld() => new EscrowNotHeldCase();
    public static EscrowReleaseError Payment(PaymentError error) => new PaymentCase(error);

    public static Option<EscrowReleaseError> FromCode(string code) => code switch
    {
        "escrow.release_not_found" => Option.Some(EscrowNotFound()),
        "escrow.release_not_held" => Option.Some(EscrowNotHeld()),
        _ => PaymentError.FromCode(code).Map(Payment)
    };
}

[Union(EnableImplicitConversions = false)]
public partial record EscrowRefundError : IError
{
    public partial record EscrowNotFoundCase;
    public partial record EscrowNotRefundableCase;
    public partial record CommissionBindingNotFoundCase;
    public partial record CurrencyMismatchCase;
    public partial record AmountMustBePositiveCase;
    public partial record AmountExceedsRemainingCase;
    public partial record ConflictCase;
    public partial record PaymentCase(PaymentError Error);

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        _ => ErrorDefinition.NotFound("escrow.refund_not_found", "Escrow not found."),
        _ => ErrorDefinition.Conflict("escrow.refund_not_allowed", "Escrow cannot be refunded in its current state."),
        _ => ErrorDefinition.NotFound("escrow.refund_commission_binding_not_found", "Commission binding not found."),
        _ => ErrorDefinition.Invalid("escrow.refund_currency_mismatch", "Refund currency does not match."),
        _ => ErrorDefinition.Invalid("escrow.refund_amount_invalid", "Refund amount must be positive."),
        _ => ErrorDefinition.Conflict("escrow.refund_amount_exceeds_remaining", "Refund amount exceeds the remaining refundable amount."),
        _ => ErrorDefinition.Conflict("escrow.refund_conflict", "Another refund changed the refundable amount."),
        payment => payment.Error.Definition);

    public static EscrowRefundError EscrowNotFound() => new EscrowNotFoundCase();
    public static EscrowRefundError EscrowNotRefundable() => new EscrowNotRefundableCase();
    public static EscrowRefundError CommissionBindingNotFound() => new CommissionBindingNotFoundCase();
    public static EscrowRefundError CurrencyMismatch() => new CurrencyMismatchCase();
    public static EscrowRefundError AmountMustBePositive() => new AmountMustBePositiveCase();
    public static EscrowRefundError AmountExceedsRemaining() => new AmountExceedsRemainingCase();
    public static EscrowRefundError Conflict() => new ConflictCase();
    public static EscrowRefundError Payment(PaymentError error) => new PaymentCase(error);

    public static Option<EscrowRefundError> FromCode(string code) => code switch
    {
        "escrow.refund_not_found" => Option.Some(EscrowNotFound()),
        "escrow.refund_not_allowed" => Option.Some(EscrowNotRefundable()),
        "escrow.refund_commission_binding_not_found" => Option.Some(CommissionBindingNotFound()),
        "escrow.refund_currency_mismatch" => Option.Some(CurrencyMismatch()),
        "escrow.refund_amount_invalid" => Option.Some(AmountMustBePositive()),
        "escrow.refund_amount_exceeds_remaining" => Option.Some(AmountExceedsRemaining()),
        "escrow.refund_conflict" => Option.Some(Conflict()),
        _ => PaymentError.FromCode(code).Map(Payment)
    };
}

[Union(EnableImplicitConversions = false)]
public partial record HoldSessionError : IError
{
    public partial record PaymentCase(PaymentError Error);
    public partial record CommissionCase(CommissionError Error);

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        payment => payment.Error.Definition,
        commission => commission.Error.Definition);

    public static HoldSessionError Payment(PaymentError error) => new PaymentCase(error);
    public static HoldSessionError Commission(CommissionError error) => new CommissionCase(error);

    public static Option<HoldSessionError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some(Payment(payment)),
            () => CommissionError.FromCode(code).Map(Commission));
}
