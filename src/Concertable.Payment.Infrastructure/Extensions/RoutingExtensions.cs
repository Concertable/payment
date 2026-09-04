using Concertable.Payment.Infrastructure.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Concertable.Payment.Infrastructure.Extensions;

public static class RoutingExtensions
{
    public static IEndpointRouteBuilder MapPaymentGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<EscrowGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<SettlementOperationsGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<PaymentReportingGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<PayoutAccountGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<CommissionPricingGrpcService>().RequireAuthorization("ServiceToken");
        endpoints.MapGrpcService<PaymentSessionOperationsGrpcService>().RequireAuthorization("ServiceToken");
        return endpoints;
    }
}
