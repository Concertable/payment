using Concertable.Payment.Infrastructure.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Concertable.Payment.Infrastructure.Extensions;

public static class RoutingExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapPaymentGrpcServices()
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
}
