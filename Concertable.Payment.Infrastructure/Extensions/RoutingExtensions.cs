using Concertable.Payment.Infrastructure.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Concertable.Payment.Infrastructure.Extensions;

public static class RoutingExtensions
{
    public static IEndpointRouteBuilder MapPaymentGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<EscrowGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<ManagerPaymentGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<CustomerPaymentGrpcService>().RequireAuthorization("ServiceToken");
        return endpoints;
    }
}
