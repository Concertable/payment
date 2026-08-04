using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record ManagerPaymentError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public partial record CommissionFailure(CommissionError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<ManagerPaymentError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some<ManagerPaymentError>(new PaymentFailure(payment)),
            () => CommissionError.FromCode(code).Match(
                commission => Option.Some<ManagerPaymentError>(new CommissionFailure(commission)),
                Option.None<ManagerPaymentError>));
}

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowDepositError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public partial record CommissionFailure(CommissionError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<EscrowDepositError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some<EscrowDepositError>(new PaymentFailure(payment)),
            () => CommissionError.FromCode(code).Match(
                commission => Option.Some<EscrowDepositError>(new CommissionFailure(commission)),
                Option.None<EscrowDepositError>));
}

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowCaptureError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public partial record CommissionFailure(CommissionError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<EscrowCaptureError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some<EscrowCaptureError>(new PaymentFailure(payment)),
            () => CommissionError.FromCode(code).Match(
                commission => Option.Some<EscrowCaptureError>(new CommissionFailure(commission)),
                Option.None<EscrowCaptureError>));
}

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowReleaseError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record EscrowNotFound
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.NotFound("escrow.release_not_found", "Escrow not found.");
    }

    public partial record EscrowNotHeld
    {
        public override ErrorDefinition Definition =>
            ErrorDefinition.Conflict("escrow.release_not_held", "Only held escrow can be released.");
    }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<EscrowReleaseError> FromCode(string code) => code switch
    {
        "escrow.release_not_found" => Option.Some<EscrowReleaseError>(new EscrowNotFound()),
        "escrow.release_not_held" => Option.Some<EscrowReleaseError>(new EscrowNotHeld()),
        _ => PaymentError.FromCode(code).Match(
            payment => Option.Some<EscrowReleaseError>(new PaymentFailure(payment)),
            Option.None<EscrowReleaseError>)
    };
}

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

[Union(EnableImplicitConversions = false)]
public abstract partial record HoldSessionError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public partial record CommissionFailure(CommissionError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<HoldSessionError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some<HoldSessionError>(new PaymentFailure(payment)),
            () => CommissionError.FromCode(code).Match(
                commission => Option.Some<HoldSessionError>(new CommissionFailure(commission)),
                Option.None<HoldSessionError>));
}
