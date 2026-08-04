using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowRefundError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record EscrowNotFound
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.NotFound("escrow.refund_not_found", "Escrow not found.");
    }

    public partial record EscrowNotRefundable
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("escrow.refund_not_allowed", "Escrow cannot be refunded in its current state.");
    }

    public partial record CommissionBindingNotFound
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.NotFound("escrow.refund_commission_binding_not_found", "Commission binding not found.");
    }

    public partial record CurrencyMismatch
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Invalid("escrow.refund_currency_mismatch", "Refund currency does not match.");
    }

    public partial record AmountMustBePositive
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Invalid("escrow.refund_amount_invalid", "Refund amount must be positive.");
    }

    public partial record AmountExceedsRemaining
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("escrow.refund_amount_exceeds_remaining", "Refund amount exceeds the remaining refundable amount.");
    }

    public partial record Conflict
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("escrow.refund_conflict", "Another refund changed the refundable amount.");
    }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<EscrowRefundError> FromCode(string code) => code switch
    {
        "escrow.refund_not_found" => Option.Some<EscrowRefundError>(new EscrowNotFound()),
        "escrow.refund_not_allowed" => Option.Some<EscrowRefundError>(new EscrowNotRefundable()),
        "escrow.refund_commission_binding_not_found" => Option.Some<EscrowRefundError>(new CommissionBindingNotFound()),
        "escrow.refund_currency_mismatch" => Option.Some<EscrowRefundError>(new CurrencyMismatch()),
        "escrow.refund_amount_invalid" => Option.Some<EscrowRefundError>(new AmountMustBePositive()),
        "escrow.refund_amount_exceeds_remaining" => Option.Some<EscrowRefundError>(new AmountExceedsRemaining()),
        "escrow.refund_conflict" => Option.Some<EscrowRefundError>(new Conflict()),
        _ => PaymentError.FromCode(code).Match(
            payment => Option.Some<EscrowRefundError>(new PaymentFailure(payment)),
            Option.None<EscrowRefundError>)
    };
}
