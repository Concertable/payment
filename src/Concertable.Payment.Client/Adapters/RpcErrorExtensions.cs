using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class RpcErrorExtensions
{
    private const string ErrorTrailer = "payment-error-bin";

    public static Proto.OperationErrorDetail? GetOperationError(this RpcException exception)
    {
        var bytes = exception.Trailers
            .FirstOrDefault(entry => entry.Key == ErrorTrailer)
            ?.ValueBytes;
        return bytes is null
            ? null
            : Proto.OperationErrorDetail.Parser.ParseFrom(bytes);
    }
}
