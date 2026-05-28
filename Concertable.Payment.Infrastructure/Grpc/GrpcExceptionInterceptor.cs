using Concertable.Kernel.Exceptions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class GrpcExceptionInterceptor : Interceptor
{
    private readonly ILogger<GrpcExceptionInterceptor> logger;

    public GrpcExceptionInterceptor(ILogger<GrpcExceptionInterceptor> logger)
    {
        this.logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException ex)
        {
            logger.GrpcHandlerRpcError(context.Method, ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (HttpException ex)
        {
            logger.GrpcHandlerError(context.Method, ex);
            throw new RpcException(new Status(ex.StatusCode.ToGrpc(), ex.Detail));
        }
        catch (Exception ex)
        {
            logger.GrpcHandlerUnhandledException(context.Method, ex);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }
}
