using Concertable.Kernel.Auth;
using Concertable.Payment.Client.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPaymentClient(IConfiguration configuration)
        {
            var address = configuration["services:payment-web:https:0"]
            ?? throw new InvalidOperationException("Payment service address (services:payment-web:https:0) is not configured.");

            AddPaymentGrpcClient<Proto.SettlementOperations.SettlementOperationsClient>(services, address);
            AddPaymentGrpcClient<Proto.PaymentReporting.PaymentReportingClient>(services, address);
            AddPaymentGrpcClient<Proto.Escrow.EscrowClient>(services, address);
            AddPaymentGrpcClient<Proto.PayoutAccount.PayoutAccountClient>(services, address);
            AddPaymentGrpcClient<Proto.CommissionPricing.CommissionPricingClient>(services, address);
            AddPaymentGrpcClient<Proto.PaymentSessionOperations.PaymentSessionOperationsClient>(services, address);

            services.AddScoped<ISettlementOperationsClient, SettlementOperationsClient>();
            services.AddScoped<IPaymentReportingClient, PaymentReportingClient>();
            services.AddScoped<EscrowClient>();
            services.AddScoped<IEscrowOperationsClient>(sp => sp.GetRequiredService<EscrowClient>());
            services.AddScoped<PayoutAccountClient>();
            services.AddScoped<IPayoutAccountOperationsClient>(sp => sp.GetRequiredService<PayoutAccountClient>());
            services.AddScoped<CommissionClient>();
            services.AddScoped<ICommissionPricingClient>(sp => sp.GetRequiredService<CommissionClient>());
            services.AddScoped<IPaymentSessionOperationsClient, PaymentSessionOperationsClient>();

            return services;
        }
    }

    private static void AddPaymentGrpcClient<TClient>(IServiceCollection services, string address)
        where TClient : class =>
        services.AddGrpcClient<TClient>(o => o.Address = new Uri(address))
            .AddCallCredentials(async (_, metadata, sp) =>
            {
                var token = await sp.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
                metadata.Add("Authorization", $"Bearer {token}");
            });
}
