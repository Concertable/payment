using Reunion.Errors;
using Reunion;
using Concertable.Payment.Grpc;
using Google.Protobuf;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class PaymentGrpcErrors
{
    public const string TrailerKey = "concertable-payment-error-bin";

    public static TValue ValueOrRpcException<TValue, TError>(this Result<TValue, TError> result)
        where TValue : notnull
        where TError : IError
    {
        if (result.TryGetValue(out var value))
            return value;

        result.TryGetError(out var error);
        throw error!.ToRpcException();
    }

    public static RpcException ToRpcException<TError>(this TError error)
        where TError : IError
    {
        var definition = error.Definition;
        var detail = new OperationErrorDetail
        {
            Code = definition.Code,
            Message = definition.Message,
            Kind = definition.Kind.ToProto()
        };
        var trailers = new Metadata { new Metadata.Entry(TrailerKey, detail.ToByteArray()) };
        return new RpcException(new Status(definition.Kind.ToGrpc(), definition.Message), trailers);
    }

    private static OperationErrorKind ToProto(this ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => OperationErrorKind.OperationErrorInvalid,
        ErrorKind.NotFound => OperationErrorKind.OperationErrorNotFound,
        ErrorKind.Conflict => OperationErrorKind.OperationErrorConflict,
        ErrorKind.Unauthenticated => OperationErrorKind.OperationErrorUnauthenticated,
        ErrorKind.Forbidden => OperationErrorKind.OperationErrorForbidden,
        ErrorKind.PaymentRequired => OperationErrorKind.OperationErrorPaymentRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static StatusCode ToGrpc(this ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCode.InvalidArgument,
        ErrorKind.NotFound => StatusCode.NotFound,
        ErrorKind.Conflict => StatusCode.Aborted,
        ErrorKind.Unauthenticated => StatusCode.Unauthenticated,
        ErrorKind.Forbidden => StatusCode.PermissionDenied,
        ErrorKind.PaymentRequired => StatusCode.FailedPrecondition,
        _ => StatusCode.Internal
    };
}
