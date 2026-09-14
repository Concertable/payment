extern alias PaymentClient;

using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Events;
using Concertable.Testing;
using System.Reflection;
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

    private static readonly string[] ProviderIdentifierTerms =
    [
        "ChargeId",
        "PaymentIntentId",
        "PaymentMethodId",
        "ProviderObjectId",
        "ProviderTransactionId",
        "RefundId",
        "SetupIntentId",
        "TransactionId",
        "TransferId"
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

    [Theory]
    [InlineData(typeof(PaymentOperationIdentity))]
    [InlineData(typeof(ClientSnapshot))]
    public void PublishedAssemblies_DoNotExposeProviderIdentifiers(Type type)
    {
        var exposedNames = type.Assembly.GetExportedTypes()
            .SelectMany(ExposedNames)
            .Where(name => ProviderIdentifierTerms.Any(term =>
                name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(exposedNames);
    }

    private static IEnumerable<string> ExposedNames(Type type)
    {
        yield return type.FullName ?? type.Name;

        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return $"{type.FullName}.{member.Name}";

            if (member is not MethodBase method)
                continue;

            foreach (var parameter in method.GetParameters())
                yield return $"{type.FullName}.{member.Name}({parameter.Name})";
        }
    }
}
