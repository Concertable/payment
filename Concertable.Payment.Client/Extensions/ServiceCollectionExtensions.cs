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

        services.AddScoped<IManagerPaymentClient, ManagerPaymentClient>();
        services.AddScoped<ICustomerPaymentClient, CustomerPaymentClient>();
        services.AddScoped<IEscrowClient, EscrowClient>();
        services.AddScoped<IPayoutAccountClient, PayoutAccountClient>();

        return services;
    }
}
