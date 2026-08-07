using Concertable.Kernel.Errors;
using Concertable.Payment.Contracts.Enums;
using Dunet;

namespace Concertable.Payment.Domain.Errors;

[Union(EnableImplicitConversions = false)]
internal partial record TransactionTransitionError : IError
{
    public ErrorDefinition Definition => this switch
    {
        NotPending(var status) => ErrorDefinition.Conflict<NotPending>(
            $"Transaction is {status} and cannot transition from pending."),
        NotComplete(var status) => ErrorDefinition.Conflict<NotComplete>(
            $"Transaction is {status}; only a complete transaction can be refunded.")
    };

    [ErrorCode("payment.transaction_not_pending")]
    public partial record NotPending(TransactionStatus Status);

    [ErrorCode("payment.transaction_not_complete")]
    public partial record NotComplete(TransactionStatus Status);
}

[Union(EnableImplicitConversions = false)]
internal partial record EscrowTransitionError : IError
{
    public ErrorDefinition Definition => this switch
    {
        NotPending(var status) => ErrorDefinition.Conflict<NotPending>(
            $"Escrow is {status} and cannot transition from pending."),
        NotHeld(var status) => ErrorDefinition.Conflict<NotHeld>(
            $"Escrow is {status}; only held escrow can be released."),
        NotRefundable(var status) => ErrorDefinition.Conflict<NotRefundable>(
            $"Escrow is {status} and cannot be refunded."),
        NotDisputable(var status) => ErrorDefinition.Conflict<NotDisputable>(
            $"Escrow is {status}; only held escrow can be disputed.")
    };

    [ErrorCode("escrow.not_pending")]
    public partial record NotPending(EscrowStatus Status);

    [ErrorCode("escrow.not_held")]
    public partial record NotHeld(EscrowStatus Status);

    [ErrorCode("escrow.not_refundable")]
    public partial record NotRefundable(EscrowStatus Status);

    [ErrorCode("escrow.not_disputable")]
    public partial record NotDisputable(EscrowStatus Status);
}

[Union(EnableImplicitConversions = false)]
internal partial record PaymentRefundTransitionError : IError
{
    public ErrorDefinition Definition => this switch
    {
        NotPending(var status) => ErrorDefinition.Conflict<NotPending>(
            $"Refund is {status} and cannot transition from pending.")
    };

    [ErrorCode("payment.refund_not_pending")]
    public partial record NotPending(PaymentRefundStatus Status);
}
