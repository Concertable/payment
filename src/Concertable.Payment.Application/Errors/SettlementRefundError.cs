using Reunion.Errors;
using Concertable.Payment.Contracts.Errors;
using Dunet;

namespace Concertable.Payment.Application.Errors;

[Union(EnableImplicitConversions = false)]
internal abstract partial record SettlementRefundError : IError
{
    public ErrorDefinition Definition => this switch
    {
        SettlementNotFound => ErrorDefinition.For<SettlementRefundError>().NotFound<SettlementNotFound>(),
        SettlementNotRefundable => ErrorDefinition.For<SettlementRefundError>().Conflict<SettlementNotRefundable>("Settlement cannot be refunded in its current state."),
        CommissionBindingNotFound => ErrorDefinition.For<SettlementRefundError>().NotFound<CommissionBindingNotFound>(),
        CurrencyMismatch => ErrorDefinition.For<SettlementRefundError>().Invalid<CurrencyMismatch>("Refund currency does not match."),
        AmountMustBePositive => ErrorDefinition.For<SettlementRefundError>().Invalid<AmountMustBePositive>("Refund amount must be positive."),
        AmountExceedsRemaining => ErrorDefinition.For<SettlementRefundError>().Conflict<AmountExceedsRemaining>("Refund amount exceeds the remaining refundable amount."),
        Conflict => ErrorDefinition.For<SettlementRefundError>().Conflict<Conflict>("Another refund changed the refundable amount."),
        PaymentFailure(var error) => error.Definition
    };

    [ErrorCode("settlement.refund_not_found")]
    public partial record SettlementNotFound;

    [ErrorCode("settlement.refund_not_allowed")]
    public partial record SettlementNotRefundable;

    public partial record CommissionBindingNotFound;

    public partial record CurrencyMismatch;

    [ErrorCode("settlement.refund_amount_invalid")]
    public partial record AmountMustBePositive;

    public partial record AmountExceedsRemaining;

    public partial record Conflict;

    public partial record PaymentFailure(PaymentError Error);
}
