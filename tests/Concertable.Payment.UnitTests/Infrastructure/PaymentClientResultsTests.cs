extern alias PaymentClient;

using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;
using Google.Protobuf;
using Grpc.Core;
using PaymentClientResults = PaymentClient::Concertable.Payment.Client.Adapters.PaymentClientResults;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentClientResultsTests
{
    [Fact]
    public async Task ExecuteAsync_Success_ReturnsValue()
    {
        var result = await PaymentClientResults.ExecuteAsync(
            () => Task.FromResult("paid"),
            PaymentError.FromCode,
            CancellationToken.None);

        Assert.True(result.TryGetValue(out var value));
        Assert.Equal("paid", value);
    }

    [Fact]
    public async Task ExecuteAsync_KnownPaymentError_ReturnsTypedFailure()
    {
        var exception = RpcFailure(Detail(
            "payment.rejected",
            "The payment was rejected.",
            kind: 5));

        var result = await PaymentClientResults.ExecuteAsync(
            () => Task.FromException<string>(exception),
            PaymentError.FromCode,
            CancellationToken.None);

        Assert.True(result.TryGetError(out var error));
        Assert.Equal(new PaymentError.PaymentRejected(), error);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPaymentError_RethrowsRpcException()
    {
        var exception = RpcFailure(Detail("payment.unknown", "Unknown.", kind: 0));

        var thrown = await Assert.ThrowsAsync<RpcException>(() => PaymentClientResults.ExecuteAsync(
            () => Task.FromException<string>(exception),
            PaymentError.FromCode,
            CancellationToken.None));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedPaymentError_RethrowsRpcException()
    {
        var exception = RpcFailure([0x0A, 0x05, 0x01]);

        var thrown = await Assert.ThrowsAsync<RpcException>(() => PaymentClientResults.ExecuteAsync(
            () => Task.FromException<string>(exception),
            PaymentError.FromCode,
            CancellationToken.None));

        Assert.Same(exception, thrown);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var exception = new RpcException(new Status(StatusCode.Cancelled, "Cancelled."));

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => PaymentClientResults.ExecuteAsync(
            () => Task.FromException<string>(exception),
            PaymentError.FromCode,
            source.Token));

        Assert.Equal(source.Token, thrown.CancellationToken);
        Assert.Same(exception, thrown.InnerException);
    }

    private static RpcException RpcFailure(byte[] detail) =>
        new(
            new Status(StatusCode.FailedPrecondition, "Failed."),
            new Metadata { new Metadata.Entry("concertable-payment-error-bin", detail) });

    private static byte[] Detail(string code, string message, int kind)
    {
        using var stream = new MemoryStream();
        using var output = new CodedOutputStream(stream, leaveOpen: true);
        output.WriteTag(1, WireFormat.WireType.LengthDelimited);
        output.WriteString(code);
        output.WriteTag(2, WireFormat.WireType.LengthDelimited);
        output.WriteString(message);
        output.WriteTag(3, WireFormat.WireType.Varint);
        output.WriteEnum(kind);
        output.Flush();
        return stream.ToArray();
    }
}
