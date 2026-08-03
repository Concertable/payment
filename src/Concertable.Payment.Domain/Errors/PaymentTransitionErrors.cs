using Concertable.Kernel.Errors;
using Concertable.Payment.Contracts.Enums;
using Dunet;

namespace Concertable.Payment.Domain.Errors;

[Union(EnableImplicitConversions = false)]
internal partial record TransactionTransitionError : IError
{
    public partial record NotPendingCase(TransactionStatus Status);
    public partial record NotCompleteCase(TransactionStatus Status);

    public ErrorDefinition Definition => Match(
        transition => ErrorDefinition.Conflict(
            "payment.transaction_not_pending",
            $"Transaction is {transition.Status} and cannot transition from pending."),
        transition => ErrorDefinition.Conflict(
            "payment.transaction_not_complete",
            $"Transaction is {transition.Status}; only a complete transaction can be refunded."));

    public static TransactionTransitionError NotPending(TransactionStatus status) =>
        new NotPendingCase(status);

    public static TransactionTransitionError NotComplete(TransactionStatus status) =>
        new NotCompleteCase(status);
}

[Union(EnableImplicitConversions = false)]
internal partial record EscrowTransitionError : IError
{
    public partial record NotPendingCase(EscrowStatus Status);
    public partial record NotHeldCase(EscrowStatus Status);
    public partial record NotRefundableCase(EscrowStatus Status);
    public partial record NotDisputableCase(EscrowStatus Status);

    public ErrorDefinition Definition => Match(
        transition => ErrorDefinition.Conflict(
            "escrow.not_pending",
            $"Escrow is {transition.Status} and cannot transition from pending."),
        transition => ErrorDefinition.Conflict(
            "escrow.not_held",
            $"Escrow is {transition.Status}; only held escrow can be released."),
        transition => ErrorDefinition.Conflict(
            "escrow.not_refundable",
            $"Escrow is {transition.Status} and cannot be refunded."),
        transition => ErrorDefinition.Conflict(
            "escrow.not_disputable",
            $"Escrow is {transition.Status}; only held escrow can be disputed."));

    public static EscrowTransitionError NotPending(EscrowStatus status) => new NotPendingCase(status);
    public static EscrowTransitionError NotHeld(EscrowStatus status) => new NotHeldCase(status);
    public static EscrowTransitionError NotRefundable(EscrowStatus status) => new NotRefundableCase(status);
    public static EscrowTransitionError NotDisputable(EscrowStatus status) => new NotDisputableCase(status);
}

[Union(EnableImplicitConversions = false)]
internal partial record PaymentRefundTransitionError : IError
{
    public partial record NotPendingCase(PaymentRefundStatus Status);

    public ErrorDefinition Definition => Match(
        transition => ErrorDefinition.Conflict(
            "payment.refund_not_pending",
            $"Refund is {transition.Status} and cannot transition from pending."));

    public static PaymentRefundTransitionError NotPending(PaymentRefundStatus status) =>
        new NotPendingCase(status);
}
