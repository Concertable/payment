using Reunion.Errors;
using Reunion;
using Concertable.Payment.Grpc;
using Google.Protobuf;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class PaymentGrpcErrors
{
    public const string TrailerKey = "concertable-payment-error-bin";

    extension<TValue, TError>(Result<TValue, TError> result)
        where TValue : notnull
        where TError : IError
    {
        public TValue ValueOrRpcException()
        {
            if (result.TryGetValue(out var value))
                return value;

            result.TryGetError(out var error);
            throw error!.ToRpcException();
        }
    }

    extension<TError>(UnitResult<TError> result) where TError : IError
    {
        public void SuccessOrRpcException()
        {
            if (result.IsSuccess)
                return;

            result.TryGetError(out var error);
            throw error!.ToRpcException();
        }
    }

    extension<TError>(TError error) where TError : IError
    {
        public RpcException ToRpcException()
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
    }

    extension(ErrorKind kind)
    {
        private OperationErrorKind ToProto() => kind switch
        {
            ErrorKind.Invalid => OperationErrorKind.OperationErrorInvalid,
            ErrorKind.NotFound => OperationErrorKind.OperationErrorNotFound,
            ErrorKind.Conflict => OperationErrorKind.OperationErrorConflict,
            ErrorKind.Unauthenticated => OperationErrorKind.OperationErrorUnauthenticated,
            ErrorKind.Forbidden => OperationErrorKind.OperationErrorForbidden,
            ErrorKind.PaymentRequired => OperationErrorKind.OperationErrorPaymentRequired,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        private StatusCode ToGrpc() => kind switch
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
}
