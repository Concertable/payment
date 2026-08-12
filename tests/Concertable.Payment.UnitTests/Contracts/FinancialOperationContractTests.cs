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
        { typeof(FinancialOperationSucceededEvent), "concertable.payment.financial-operation-succeeded.v1" },
        { typeof(FinancialOperationRejectedEvent), "concertable.payment.financial-operation-rejected.v1" },
        { typeof(FinancialOperationDeferredEvent), "concertable.payment.financial-operation-deferred.v1" }
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
