using Concertable.Kernel.Errors;
namespace Concertable.Payment.Contracts.Errors;

public sealed record RefundError(ErrorDefinition Definition) : IError
{
    public static readonly RefundError EscrowNotFound = new(
        ErrorDefinition.NotFound("payment.escrow_not_found", "The escrow payment was not found."));

    public static readonly RefundError InvalidEscrowState = new(
        ErrorDefinition.Conflict("payment.escrow_refund_invalid_state", "The escrow payment cannot be refunded in its current state."));

    public static readonly RefundError CurrencyMismatch = new(
        ErrorDefinition.Invalid("payment.refund_currency_mismatch", "The refund currency does not match the payment."));

    public static readonly RefundError InvalidAmount = new(
        ErrorDefinition.Invalid("payment.refund_amount_invalid", "The refund amount is invalid."));

    public static readonly RefundError CommissionBindingNotFound = new(
        ErrorDefinition.NotFound("payment.commission_binding_not_found", "The commission binding was not found."));

    public static readonly RefundError RefundRejected = new(
        ErrorDefinition.Invalid("payment.refund_rejected", "The refund was rejected."));
}
