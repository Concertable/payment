using Concertable.Kernel.Errors;

namespace Concertable.Payment.Application.Errors;

internal sealed record SettlementRefundError(ErrorDefinition Definition) : IError
{
    public static readonly SettlementRefundError SettlementNotFound = new(
        ErrorDefinition.NotFound(
            "settlement.refund_not_found",
            "Settlement not found."));

    public static readonly SettlementRefundError SettlementNotRefundable = new(
        ErrorDefinition.Conflict(
            "settlement.refund_not_allowed",
            "Settlement cannot be refunded in its current state."));

    public static readonly SettlementRefundError CommissionBindingNotFound = new(
        ErrorDefinition.NotFound(
            "settlement.refund_commission_binding_not_found",
            "Commission binding not found."));

    public static readonly SettlementRefundError CurrencyMismatch = new(
        ErrorDefinition.Invalid(
            "settlement.refund_currency_mismatch",
            "Refund currency does not match."));

    public static readonly SettlementRefundError AmountMustBePositive = new(
        ErrorDefinition.Invalid(
            "settlement.refund_amount_invalid",
            "Refund amount must be positive."));

    public static readonly SettlementRefundError AmountExceedsRemaining = new(
        ErrorDefinition.Conflict(
            "settlement.refund_amount_exceeds_remaining",
            "Refund amount exceeds the remaining refundable amount."));

    public static readonly SettlementRefundError Conflict = new(
        ErrorDefinition.Conflict(
            "settlement.refund_conflict",
            "Another refund changed the refundable amount."));
}
