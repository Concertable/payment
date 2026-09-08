using Concertable.Kernel.Auth;
using Concertable.Payment.Client.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Extensions;

public static class ServiceCollectionExtensions
{
    private const string AllowInsecureHttpConfigurationKey = "PaymentClient:AllowInsecureHttp";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddPaymentClient(IConfiguration configuration)
        {
            var address = configuration["services:payment-web:grpc:0"]
                ?? configuration["services:payment-web:https:0"]
                ?? throw new InvalidOperationException(
                    "Payment service address (services:payment-web:grpc:0 or services:payment-web:https:0) is not configured.");
            var uri = new Uri(address);

            if (uri.Scheme == Uri.UriSchemeHttp
                && !string.Equals(
                    configuration[AllowInsecureHttpConfigurationKey],
                    bool.TrueString,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cleartext Payment transport requires {AllowInsecureHttpConfigurationKey}=true "
                    + "in an explicitly trusted composition.");
            }

            AddPaymentGrpcClient<Proto.SettlementOperations.SettlementOperationsClient>(services, uri);
            AddPaymentGrpcClient<Proto.PaymentReporting.PaymentReportingClient>(services, uri);
            AddPaymentGrpcClient<Proto.Escrow.EscrowClient>(services, uri);
            AddPaymentGrpcClient<Proto.PayoutAccount.PayoutAccountClient>(services, uri);
            AddPaymentGrpcClient<Proto.CommissionPricing.CommissionPricingClient>(services, uri);
            AddPaymentGrpcClient<Proto.PaymentSessionOperations.PaymentSessionOperationsClient>(services, uri);

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

    private static void AddPaymentGrpcClient<TClient>(IServiceCollection services, Uri address)
        where TClient : class
    {
        var client = services.AddGrpcClient<TClient>(options => options.Address = address);

        if (address.Scheme == Uri.UriSchemeHttp)
            client.ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true);

        client.AddCallCredentials(async (_, metadata, serviceProvider) =>
        {
            var token = await serviceProvider.GetRequiredService<ITokenService>().GetTokenAsync("payment:write");
            metadata.Add("Authorization", $"Bearer {token}");
        });
    }
}
