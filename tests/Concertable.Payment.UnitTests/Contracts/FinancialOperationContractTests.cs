using Concertable.Messaging.Contracts;
using Concertable.Payment.Contracts;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class FinancialOperationContractTests
{
    public static TheoryData<Type, string> MessageTypes => new()
    {
        { typeof(CaptureEscrowCommand), "concertable.payment.capture-escrow.v1" },
        { typeof(DepositEscrowCommand), "concertable.payment.deposit-escrow.v1" },
        { typeof(RefundEscrowCommand), "concertable.payment.refund-escrow.v1" },
        { typeof(CaptureEscrowSucceededEvent), "concertable.payment.capture-escrow-succeeded.v1" },
        { typeof(CaptureEscrowRejectedEvent), "concertable.payment.capture-escrow-rejected.v1" },
        { typeof(DepositEscrowSucceededEvent), "concertable.payment.deposit-escrow-succeeded.v1" },
        { typeof(DepositEscrowRejectedEvent), "concertable.payment.deposit-escrow-rejected.v1" },
        { typeof(RefundEscrowSucceededEvent), "concertable.payment.refund-escrow-succeeded.v1" },
        { typeof(RefundEscrowRejectedEvent), "concertable.payment.refund-escrow-rejected.v1" },
        { typeof(RefundEscrowDeferredEvent), "concertable.payment.refund-escrow-deferred.v1" }
    };

    [Theory]
    [MemberData(nameof(MessageTypes))]
    public void MessageType_ReturnsPublishedContract(Type type, string expected) =>
        Assert.Equal(expected, MessageTypeAttribute.Resolve(type));

    [Fact]
    public void ContractsAssembly_DoesNotReferenceConsumerRuntime()
    {
        var references = typeof(CaptureEscrowCommand).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(references, name => name?.StartsWith("Concertable.B2B", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(references, name => name?.StartsWith("Concertable.Customer", StringComparison.Ordinal) == true);
    }
}
