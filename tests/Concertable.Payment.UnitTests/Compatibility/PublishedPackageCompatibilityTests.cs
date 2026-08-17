extern alias PaymentClient;

using System.Reflection;
using System.Xml.Linq;
using Concertable.Payment.Contracts.Events;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using ClientSnapshot = PaymentClient::Concertable.Payment.Client.PaymentOperationSnapshot;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Compatibility;

public sealed class PublishedPackageCompatibilityTests
{
    private const string BaselineVersion = "0.1.0-alpha.0.1009";

    [Fact]
    public void ContractsPublicApi_CurrentSurfaceIsAdditive() =>
        AssertAdditiveBaseline(
            "Concertable.Payment.Contracts.public-api.txt",
            PublicApiSnapshot.Create(typeof(PaymentOperationStateChanged).Assembly));

    [Fact]
    public void ClientPublicApi_CurrentSurfaceIsAdditive() =>
        AssertAdditiveBaseline(
            "Concertable.Payment.Client.public-api.txt",
            PublicApiSnapshot.Create(typeof(ClientSnapshot).Assembly));

    [Fact]
    public void MessageUrns_CurrentSurfacePreservesPublishedValues() =>
        AssertAdditiveBaseline(
            "Concertable.Payment.Contracts.message-urns.txt",
            PublicApiSnapshot.CreateMessageUrns(typeof(PaymentOperationStateChanged).Assembly));

    [Fact]
    public void ProtobufDescriptor_CurrentSchemaIsAdditive()
    {
        var baseline = FileDescriptorSet.Parser.ParseFrom(Convert.FromBase64String(File.ReadAllText(BaselinePath("payment.protoset.base64")).Trim()));
        var baselineRows = ProtoSchemaSnapshot.Create(Assert.Single(baseline.File));
        var candidateRows = ProtoSchemaSnapshot.Create(Proto.PaymentReflection.Descriptor.ToProto());

        Assert.Empty(baselineRows.Except(candidateRows, StringComparer.Ordinal));
    }

    [Fact]
    public void PublishedAssemblies_DoNotReferenceProviderOrConsumerRuntime()
    {
        var assemblies = new[]
        {
            typeof(PaymentOperationStateChanged).Assembly,
            typeof(ClientSnapshot).Assembly
        };

        var forbidden = assemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name)
            .Where(name => name is not null && (name.StartsWith("Stripe", StringComparison.Ordinal)
                || name.StartsWith("Concertable.B2B", StringComparison.Ordinal)
                || name.StartsWith("Concertable.Customer", StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void PaymentDeployableProjects_DoNotReferenceConsumerAssemblies()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var forbidden = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".AppHost", StringComparison.Ordinal)
                && !Path.GetFileNameWithoutExtension(path).EndsWith(".Hosting", StringComparison.Ordinal))
            .SelectMany(path => XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
                .Select(element => new
                {
                    Project = Path.GetRelativePath(sourceRoot, path),
                    Reference = (string?)element.Attribute("Include")
                }))
            .Where(item => item.Reference is not null && (item.Reference.Contains("Concertable.B2B", StringComparison.Ordinal)
                || item.Reference.Contains("Concertable.Customer", StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }

    private static void AssertAdditiveBaseline(string fileName, IEnumerable<string> candidate)
    {
        var baseline = File.ReadAllLines(BaselinePath(fileName));
        var missing = baseline.Except(candidate, StringComparer.Ordinal).ToArray();

        Assert.Empty(missing);
    }

    private static string BaselinePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Compatibility", "Baselines", BaselineVersion, fileName);
}
