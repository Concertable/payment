using Reunion.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowRefundError : IError
{
    public ErrorDefinition Definition => this switch
    {
        EscrowNotFound => ErrorDefinition.For<EscrowRefundError>().NotFound<EscrowNotFound>(),
        EscrowNotRefundable => ErrorDefinition.For<EscrowRefundError>().Conflict<EscrowNotRefundable>("Escrow cannot be refunded in its current state."),
        CommissionBindingNotFound => ErrorDefinition.For<EscrowRefundError>().NotFound<CommissionBindingNotFound>(),
        CurrencyMismatch => ErrorDefinition.For<EscrowRefundError>().Invalid<CurrencyMismatch>("Refund currency does not match."),
        AmountMustBePositive => ErrorDefinition.For<EscrowRefundError>().Invalid<AmountMustBePositive>("Refund amount must be positive."),
        AmountExceedsRemaining => ErrorDefinition.For<EscrowRefundError>().Conflict<AmountExceedsRemaining>("Refund amount exceeds the remaining refundable amount."),
        Conflict => ErrorDefinition.For<EscrowRefundError>().Conflict<Conflict>("Another refund changed the refundable amount."),
        PaymentFailure(var error) => error.Definition
    };

    public partial record EscrowNotFound;

    [ErrorCode("escrow.refund_not_allowed")]
    public partial record EscrowNotRefundable;

    public partial record CommissionBindingNotFound;

    public partial record CurrencyMismatch;

    [ErrorCode("escrow.refund_amount_invalid")]
    public partial record AmountMustBePositive;

    public partial record AmountExceedsRemaining;

    public partial record Conflict;

    public partial record PaymentFailure(PaymentError Error);
}
