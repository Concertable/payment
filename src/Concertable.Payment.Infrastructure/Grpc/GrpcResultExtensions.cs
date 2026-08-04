using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Concertable.Payment.Grpc;
using Google.Protobuf;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class GrpcResultExtensions
{
    private const string ErrorTrailer = "payment-error-bin";

    public static RpcException ToRpcException<TError>(this TError error)
        where TError : IError
    {
        var definition = error.Definition;
        var detail = new OperationErrorDetail
        {
            Code = definition.Code,
            Message = definition.Message
        };
        var trailers = new Metadata
        {
            { ErrorTrailer, detail.ToByteArray() }
        };
        return new RpcException(
            new Status(StatusCode.FailedPrecondition, definition.Message),
            trailers);
    }

    public static TValue GetValueOrThrow<TValue, TError>(this Result<TValue, TError> result)
        where TValue : notnull
        where TError : IError =>
        result.Match(
            value => value,
            error => throw error.ToRpcException());
}
