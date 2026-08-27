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

        AddPaymentGrpcClient<Proto.ManagerPayment.ManagerPaymentClient>(services, address);
        AddPaymentGrpcClient<Proto.CustomerPayment.CustomerPaymentClient>(services, address);
        AddPaymentGrpcClient<Proto.Escrow.EscrowClient>(services, address);
        AddPaymentGrpcClient<Proto.PayoutAccount.PayoutAccountClient>(services, address);
        AddPaymentGrpcClient<Proto.CommissionPricing.CommissionPricingClient>(services, address);
        AddPaymentGrpcClient<Proto.PaymentSessionOperations.PaymentSessionOperationsClient>(services, address);

        services.AddScoped<ManagerPaymentClient>();
        services.AddScoped<IManagerPaymentOperationsClient>(sp => sp.GetRequiredService<ManagerPaymentClient>());
        services.AddScoped<IManagerPaymentReportingClient>(sp => sp.GetRequiredService<ManagerPaymentClient>());
        services.AddScoped<CustomerPaymentClient>();
        services.AddScoped<ICustomerPaymentOperationsClient>(sp => sp.GetRequiredService<CustomerPaymentClient>());
        services.AddScoped<EscrowClient>();
        services.AddScoped<IEscrowOperationsClient>(sp => sp.GetRequiredService<EscrowClient>());
        services.AddScoped<PayoutAccountClient>();
        services.AddScoped<IPayoutAccountOperationsClient>(sp => sp.GetRequiredService<PayoutAccountClient>());
        services.AddScoped<CommissionClient>();
        services.AddScoped<ICommissionPricingClient>(sp => sp.GetRequiredService<CommissionClient>());
        services.AddScoped<IPaymentSessionOperationsClient, PaymentSessionOperationsClient>();

        return services;
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
