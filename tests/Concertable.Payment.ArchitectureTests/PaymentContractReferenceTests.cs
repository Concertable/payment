extern alias PaymentClient;

using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Events;
using Concertable.Testing;
using Xunit;
using ClientSnapshot = PaymentClient::Concertable.Payment.Client.PaymentOperationSnapshot;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentContractReferenceTests
{
    private static readonly string[] AllowedConcertableReferences =
    [
        "Concertable.Grpc",
        "Concertable.Kernel",
        "Concertable.Messaging.Contracts",
        "Concertable.Payment.Contracts"
    ];

    [Theory]
    [InlineData(typeof(CaptureEscrowCommand))]
    [InlineData(typeof(PaymentOperationIdentity))]
    [InlineData(typeof(PaymentOperationStateChanged))]
    [InlineData(typeof(ClientSnapshot))]
    public void PublishedAssemblies_ReferenceOnlySharedPaymentDependencies(Type type)
    {
        var unexpected = type.Assembly.ReferencedAssemblyNames()
            .Where(name => name.StartsWith("Concertable.", StringComparison.Ordinal))
            .Except(AllowedConcertableReferences, StringComparer.Ordinal);

        Assert.Empty(unexpected);
    }

    [Theory]
    [InlineData(typeof(PaymentOperationIdentity))]
    [InlineData(typeof(ClientSnapshot))]
    public void PublishedAssemblies_DoNotReferenceProviderRuntime(Type type) =>
        Assert.Empty(type.Assembly.ReferencesToAssembliesStartingWith("Stripe"));
}
