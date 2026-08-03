using Concertable.Kernel.Auth;
using Concertable.Payment.Client.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentClient(this IServiceCollection services, IConfiguration configuration)
    {
        var address = configuration["services:payment-web:https:0"]
            ?? throw new InvalidOperationException("Payment service address (services:payment-web:https:0) is not configured.");

        services.AddGrpcClient<Proto.ManagerPayment.ManagerPaymentClient>(o => o.Address = new Uri(address))
            .AddCallCredentials(async (_, metadata, sp) =>
            {
                var token = await sp.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
                metadata.Add("Authorization", $"Bearer {token}");
            });

        services.AddGrpcClient<Proto.CustomerPayment.CustomerPaymentClient>(o => o.Address = new Uri(address))
            .AddCallCredentials(async (_, metadata, sp) =>
            {
                var token = await sp.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
                metadata.Add("Authorization", $"Bearer {token}");
            });

        services.AddGrpcClient<Proto.Escrow.EscrowClient>(o => o.Address = new Uri(address))
            .AddCallCredentials(async (_, metadata, sp) =>
            {
                var token = await sp.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
                metadata.Add("Authorization", $"Bearer {token}");
            });

        services.AddGrpcClient<Proto.PayoutAccount.PayoutAccountClient>(o => o.Address = new Uri(address))
            .AddCallCredentials(async (_, metadata, sp) =>
            {
                var token = await sp.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
                metadata.Add("Authorization", $"Bearer {token}");
            });

        services.AddGrpcClient<Proto.CommissionPricing.CommissionPricingClient>(o => o.Address = new Uri(address))
            .AddCallCredentials(async (_, metadata, sp) =>
            {
                var token = await sp.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
                metadata.Add("Authorization", $"Bearer {token}");
            });

        services.AddScoped<ManagerPaymentClient>();
        services.AddScoped<IManagerPaymentOperationsClient>(sp => sp.GetRequiredService<ManagerPaymentClient>());
        services.AddScoped<IManagerPaymentClient>(sp => sp.GetRequiredService<ManagerPaymentClient>());
        services.AddScoped<CustomerPaymentClient>();
        services.AddScoped<ICustomerPaymentOperationsClient>(sp => sp.GetRequiredService<CustomerPaymentClient>());
        services.AddScoped<ICustomerPaymentClient>(sp => sp.GetRequiredService<CustomerPaymentClient>());
        services.AddScoped<EscrowClient>();
        services.AddScoped<IEscrowOperationsClient>(sp => sp.GetRequiredService<EscrowClient>());
        services.AddScoped<IEscrowClient>(sp => sp.GetRequiredService<EscrowClient>());
        services.AddScoped<PayoutAccountClient>();
        services.AddScoped<IPayoutAccountOperationsClient>(sp => sp.GetRequiredService<PayoutAccountClient>());
        services.AddScoped<IPayoutAccountClient>(sp => sp.GetRequiredService<PayoutAccountClient>());
        services.AddScoped<CommissionClient>();
        services.AddScoped<ICommissionPricingClient>(sp => sp.GetRequiredService<CommissionClient>());
        services.AddScoped<ICommissionClient>(sp => sp.GetRequiredService<CommissionClient>());

        return services;
    }
}
