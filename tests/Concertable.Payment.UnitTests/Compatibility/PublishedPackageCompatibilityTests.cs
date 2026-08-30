extern alias PaymentClient;

using System.Reflection;
using Concertable.Payment.Contracts.Events;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using ClientSnapshot = PaymentClient::Concertable.Payment.Client.PaymentOperationSnapshot;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Compatibility;

public sealed class PublishedPackageCompatibilityTests
{
    private const string BaselineVersion = "0.1.0-alpha.0.1254";

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

    private static void AssertAdditiveBaseline(string fileName, IEnumerable<string> candidate)
    {
        var baseline = File.ReadAllLines(BaselinePath(fileName));
        var missing = baseline.Except(candidate, StringComparer.Ordinal).ToArray();

        Assert.Empty(missing);
    }

    private static string BaselinePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Compatibility", "Baselines", BaselineVersion, fileName);
}
