extern alias PaymentClient;

using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Grpc;
using Google.Protobuf;
using Grpc.Core;
using RpcErrorExtensions = PaymentClient::Concertable.Payment.Client.Adapters.RpcErrorExtensions;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class RpcErrorExtensionsTests
{
    [Fact]
    public void ToPaymentError_StructuredDetail_RestoresTypedCase()
    {
        var exception = ExceptionFor(new PaymentError.PayerNotFound());

        var error = RpcErrorExtensions.ToPaymentError(exception);

        Assert.Equal(new PaymentError.PayerNotFound(), error);
    }

    [Fact]
    public void ToPaymentError_LegacyOnlyDetail_UsesSafeFallback()
    {
        var exception = new RpcException(
            new Status(StatusCode.FailedPrecondition, "Legacy failure."));

        var error = RpcErrorExtensions.ToPaymentError(exception);

        Assert.Equal(new PaymentError.PaymentRejected(), error);
    }

    private static RpcException ExceptionFor(Concertable.Kernel.Errors.IError error)
    {
        var detail = new OperationErrorDetail
        {
            Code = error.Definition.Code,
            Message = error.Definition.Message
        };
        return new RpcException(
            new Status(StatusCode.FailedPrecondition, error.Definition.Message),
            new Metadata { { "payment-error-bin", detail.ToByteArray() } });
    }
}
