using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union]
public partial record RefundError : IError
{
    partial record EscrowNotFound;
    partial record InvalidEscrowState;
    partial record CurrencyMismatch;
    partial record InvalidAmount;
    partial record CommissionBindingNotFound;
    partial record RefundRejected;

    public static RefundError NotFound() => new EscrowNotFound();
    public static RefundError InvalidState() => new InvalidEscrowState();
    public static RefundError InvalidCurrency() => new CurrencyMismatch();
    public static RefundError AmountInvalid() => new InvalidAmount();
    public static RefundError MissingCommissionBinding() => new CommissionBindingNotFound();
    public static RefundError Rejected() => new RefundRejected();

    public ErrorDefinition Definition => Match<ErrorDefinition>(
        escrowNotFound => ErrorDefinition.NotFound("payment.escrow_not_found", "The escrow payment was not found."),
        invalidEscrowState => ErrorDefinition.Conflict("payment.escrow_refund_invalid_state", "The escrow payment cannot be refunded in its current state."),
        currencyMismatch => ErrorDefinition.Invalid("payment.refund_currency_mismatch", "The refund currency does not match the payment."),
        invalidAmount => ErrorDefinition.Invalid("payment.refund_amount_invalid", "The refund amount is invalid."),
        commissionBindingNotFound => ErrorDefinition.NotFound("payment.commission_binding_not_found", "The commission binding was not found."),
        refundRejected => ErrorDefinition.Invalid("payment.refund_rejected", "The refund was rejected."));
}
