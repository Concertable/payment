using Grpc.Core;
using System.Net;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class HttpStatusCodeExtensions
{
    public static StatusCode ToGrpc(this HttpStatusCode httpStatus) => httpStatus switch
    {
        HttpStatusCode.NotFound => StatusCode.NotFound,
        HttpStatusCode.BadRequest => StatusCode.InvalidArgument,
        HttpStatusCode.Unauthorized => StatusCode.Unauthenticated,
        HttpStatusCode.Forbidden => StatusCode.PermissionDenied,
        HttpStatusCode.Conflict => StatusCode.AlreadyExists,
        _ => StatusCode.Internal
    };
}
