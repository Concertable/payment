using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Google.Protobuf;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentClientResults
{
    private const string TrailerKey = "concertable-payment-error-bin";

    public static async Task<Result<TValue, TError>> ExecuteAsync<TValue, TError>(
        Func<Task<TValue>> operation,
        Func<string, Option<TError>> errorFromCode,
        CancellationToken ct)
        where TValue : notnull
        where TError : notnull
    {
        try
        {
            return Result<TValue, TError>.Success(await operation());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
        {
            throw new OperationCanceledException("The payment operation was cancelled.", ex, ct);
        }
        catch (RpcException ex)
        {
            var entry = ex.Trailers.FirstOrDefault(item => item.Key == TrailerKey && item.IsBinary);
            if (entry is null)
                throw;

            if (!ParseDetail(entry.ValueBytes).TryGetValue(out var detail))
                throw;

            if (!errorFromCode(detail.Code).TryGetValue(out var error))
                throw;
            return Result<TValue, TError>.Failure(error);
        }
    }

    public static FluentResults.Result<TValue> ToLegacy<TValue, TError>(
        this Result<TValue, TError> result)
        where TValue : notnull
        where TError : IError =>
        result.Match(
            FluentResults.Result.Ok,
            error => FluentResults.Result.Fail<TValue>(error.Definition.Message));

    public static FluentResults.Result<TValue?> ToLegacyNullable<TValue, TError>(
        this Result<Option<TValue>, TError> result)
        where TValue : class
        where TError : IError =>
        result.Match(
            value => FluentResults.Result.Ok<TValue?>(value.Match<TValue?>(item => item, () => null)),
            error => FluentResults.Result.Fail<TValue?>(error.Definition.Message));

    private static Option<Proto.OperationErrorDetail> ParseDetail(byte[] bytes)
    {
        try
        {
            return Option.Some(Proto.OperationErrorDetail.Parser.ParseFrom(bytes));
        }
        catch (InvalidProtocolBufferException)
        {
            return Option.None<Proto.OperationErrorDetail>();
        }
    }
}
