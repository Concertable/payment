using Concertable.Grpc;
using Reunion;
using Grpc.Core;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentClientResults
{
    public static async Task<Result<TValue, TError>> ExecuteAsync<TValue, TError>(
        Func<Task<TValue>> operation,
        Func<RpcException, TError> toError,
        CancellationToken ct)
        where TValue : notnull
        where TError : notnull
    {
        try
        {
            return Result<TValue, TError>.Success(await operation());
        }
        catch (RpcException ex) when (ex.IsClientCancellation(ct))
        {
            throw new OperationCanceledException("The payment operation was cancelled.", ex, ct);
        }
        catch (RpcException ex) when (ex.HasOperationErrorDetail())
        {
            return Result<TValue, TError>.Failure(toError(ex));
        }
    }
}
