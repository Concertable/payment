extern alias PaymentClient;

using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Events;
using Concertable.Testing;
using Xunit;
using ClientSnapshot = PaymentClient::Concertable.Payment.Client.PaymentOperationSnapshot;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentContractReferenceTests
{
    [Fact]
    public void ContractsAssembly_DoesNotReferenceConsumerRuntime() =>
        Assert.Empty(typeof(CaptureEscrowCommand).Assembly
            .ReferencesToAssembliesStartingWith("Concertable.B2B", "Concertable.Customer"));

    [Theory]
    [InlineData(typeof(PaymentOperationIdentity))]
    [InlineData(typeof(PaymentOperationStateChanged))]
    [InlineData(typeof(ClientSnapshot))]
    public void PublishedVocabulary_DoesNotReferenceProviderOrConsumerRuntime(Type type) =>
        Assert.Empty(type.Assembly
            .ReferencesToAssembliesStartingWith("Stripe", "Concertable.B2B", "Concertable.Customer"));
}
