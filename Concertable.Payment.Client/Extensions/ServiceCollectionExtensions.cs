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
        var address = configuration["services__payment-web__https__0"] ?? "https://payment";

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

        services.AddScoped<IManagerPaymentClient, ManagerPaymentClient>();
        services.AddScoped<ICustomerPaymentClient, CustomerPaymentClient>();
        services.AddScoped<IEscrowClient, EscrowClient>();

        return services;
    }
}
