using Concertable.Kernel.Errors;
using Concertable.Payment.Domain.Errors;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentTransitionErrorDefinitionTests
{
    public static TheoryData<IError, string, string, ErrorKind> Cases => new()
    {
        { TransactionTransitionError.NotPending(TransactionStatus.Complete), "payment.transaction_not_pending", "Transaction is Complete and cannot transition from pending.", ErrorKind.Conflict },
        { TransactionTransitionError.NotComplete(TransactionStatus.Pending), "payment.transaction_not_complete", "Transaction is Pending; only a complete transaction can be refunded.", ErrorKind.Conflict },
        { EscrowTransitionError.NotPending(EscrowStatus.Held), "escrow.not_pending", "Escrow is Held and cannot transition from pending.", ErrorKind.Conflict },
        { EscrowTransitionError.NotHeld(EscrowStatus.Pending), "escrow.not_held", "Escrow is Pending; only held escrow can be released.", ErrorKind.Conflict },
        { EscrowTransitionError.NotRefundable(EscrowStatus.Failed), "escrow.not_refundable", "Escrow is Failed and cannot be refunded.", ErrorKind.Conflict },
        { EscrowTransitionError.NotDisputable(EscrowStatus.Released), "escrow.not_disputable", "Escrow is Released; only held escrow can be disputed.", ErrorKind.Conflict },
        { PaymentRefundTransitionError.NotPending(PaymentRefundStatus.Completed), "payment.refund_not_pending", "Refund is Completed and cannot transition from pending.", ErrorKind.Conflict }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Definition_ReturnsDomainContract(
        IError error,
        string expectedCode,
        string expectedMessage,
        ErrorKind expectedKind)
    {
        var definition = error.Definition;

        Assert.Equal(expectedCode, definition.Code);
        Assert.Equal(expectedMessage, definition.Message);
        Assert.Equal(expectedKind, definition.Kind);
    }
}
