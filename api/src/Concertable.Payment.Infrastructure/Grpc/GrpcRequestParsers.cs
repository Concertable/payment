using System.Globalization;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class GrpcRequestParsers
{
    public static T ParseOrThrow<T>(this string value, string fieldName) where T : IParsable<T> =>
        T.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} '{value}' is not a valid {typeof(T).Name}."));
}
