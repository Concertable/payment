using Concertable.DataAccess;
using Concertable.Seeding;
using Concertable.User.Contracts.Events;
using Concertable.Payment.Application.Interfaces;
using Concertable.DataAccess.Infrastructure;
using Concertable.DataAccess.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Data.Seeders;
using Concertable.Payment.Infrastructure.Events;
using Concertable.Payment.Infrastructure.Handlers;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>((sp, opts) =>
            opts.UseSqlServer(configuration.GetConnectionString("PaymentDb"))
                .AddInterceptors(
                    sp.GetRequiredService<AuditInterceptor>(),
                    sp.GetRequiredService<DomainEventDispatchInterceptor>()));

        services.AddSingleton<PaymentConfigurationProvider>();
        services.AddSingleton<IEntityTypeConfigurationProvider>(sp => sp.GetRequiredService<PaymentConfigurationProvider>());

        services.Configure<StripeSettings>(configuration.GetSection("Stripe"));

        // Repositories + mappers
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IStripeEventRepository, StripeEventRepository>();
        services.AddScoped<IPayoutAccountRepository, PayoutAccountRepository>();
        services.AddScoped<IEscrowRepository, EscrowRepository>();
        services.AddSingleton<ITransactionMapper, TransactionMapper>();

        // Transaction service
        services.AddScoped<ITransactionService, TransactionService>();

        // Stripe real/fake toggle
        var useRealStripe = configuration.GetSection("ExternalServices").GetValue<bool>("UseRealStripe");
        if (useRealStripe)
        {
            services.AddSingleton<Stripe.AccountService>();
            services.AddSingleton<Stripe.AccountLinkService>();
            services.AddSingleton<Stripe.CustomerService>();
            services.AddSingleton<Stripe.PaymentMethodService>();
            services.AddSingleton<Stripe.SetupIntentService>();
            services.AddSingleton<Stripe.PaymentIntentService>();
            services.AddSingleton<Stripe.CustomerSessionService>();
            services.AddSingleton<Stripe.TransferService>();
            services.AddSingleton<Stripe.RefundService>();
            services.AddSingleton<Stripe.TransferReversalService>();
            services.AddScoped<IStripeAccountClient, StripeAccountClient>();
            services.AddScoped<IStripeHoldClient, StripeHoldClient>();
            services.AddSingleton<IStripeApiClient, StripeApiClient>();
            services.AddKeyedSingleton<IPaymentSessionConfigurator, OnSessionConfigurator>(PaymentSession.OnSession);
            services.AddKeyedSingleton<IPaymentSessionConfigurator, OffSessionConfigurator>(PaymentSession.OffSession);
            services.AddKeyedScoped<IStripePaymentIntentClient>(PaymentSession.OnSession, (sp, _) =>
                new StripePaymentIntentClient(
                    sp.GetRequiredService<IStripeApiClient>(),
                    sp.GetRequiredService<IStripeAccountClient>(),
                    sp.GetRequiredKeyedService<IPaymentSessionConfigurator>(PaymentSession.OnSession),
                    sp.GetRequiredService<ILogger<StripePaymentIntentClient>>()));
            services.AddKeyedScoped<IStripePaymentIntentClient>(PaymentSession.OffSession, (sp, _) =>
                new StripePaymentIntentClient(
                    sp.GetRequiredService<IStripeApiClient>(),
                    sp.GetRequiredService<IStripeAccountClient>(),
                    sp.GetRequiredKeyedService<IPaymentSessionConfigurator>(PaymentSession.OffSession),
                    sp.GetRequiredService<ILogger<StripePaymentIntentClient>>()));
            services.AddScoped<IStripeTransferClient, StripeTransferClient>();
            services.AddScoped<IWebhookService, WebhookService>();
        }
        else
        {
            services.AddScoped<IStripeAccountClient, FakeStripeAccountClient>();
            services.AddScoped<IStripeHoldClient, FakeStripeHoldClient>();
            services.AddKeyedScoped<IStripePaymentIntentClient, FakeStripePaymentIntentClient>(PaymentSession.OnSession);
            services.AddKeyedScoped<IStripePaymentIntentClient, FakeStripePaymentIntentClient>(PaymentSession.OffSession);
            services.AddScoped<IStripeTransferClient, FakeStripeTransferClient>();
            services.AddScoped<IWebhookService, FakeWebhookService>();
        }

        services.AddScoped<IStripePaymentIntentClientFactory, StripePaymentIntentClientFactory>();
        services.AddScoped<IPaymentManager, PaymentManager>();

        // Stripe validation (keyed by ContractType) â€” used by Concert eligibility checks
        services.AddScoped<StripeAccountValidator>();
        services.AddScoped<StripeCustomerValidator>();
        services.AddScoped<IStripeValidator, StripeValidator>();
        services.AddScoped<IStripeValidationFactory, StripeValidationFactory>();
        services.AddKeyedScoped<IStripeValidationStrategy, StripeAccountValidator>(ContractType.VenueHire);
        services.AddKeyedScoped<IStripeValidationStrategy, StripeCustomerValidator>(ContractType.FlatFee);
        services.AddKeyedScoped<IStripeValidationStrategy, StripeCustomerValidator>(ContractType.DoorSplit);
        services.AddKeyedScoped<IStripeValidationStrategy, StripeCustomerValidator>(ContractType.Versus);

        // Webhook infrastructure
        services.AddScoped<IWebhookProcessor, WebhookProcessor>();
        services.AddScoped<IWebhookQueue, WebhookQueue>();

        services.AddScoped<IManagerPaymentService, ManagerPaymentService>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<IEscrowService, EscrowService>();

        // Integration event handlers
        services.AddScoped<IIntegrationEventHandler<CustomerRegisteredEvent>, CustomerRegisteredHandler>();
        services.AddScoped<IIntegrationEventHandler<VenueManagerRegisteredEvent>, ManagerRegisteredHandler>();
        services.AddScoped<IIntegrationEventHandler<ArtistManagerRegisteredEvent>, ManagerRegisteredHandler>();
        services.AddScoped<IIntegrationEventHandler<PaymentSucceededEvent>, PaymentTransactionHandler>();
        services.AddScoped<IIntegrationEventHandler<PaymentFailedEvent>, PaymentFailureDispatcher>();
        services.AddScoped<ITransactionHandlerFactory, TransactionHandlerFactory>();
        services.AddScoped<IPaymentFailureHandlerFactory, PaymentFailureHandlerFactory>();
        services.AddKeyedScoped<ITransactionHandler, TicketTransactionHandler>(TransactionTypes.Ticket);
        services.AddKeyedScoped<ITransactionHandler, SettlementTransactionHandler>(TransactionTypes.Settlement);
        services.AddKeyedScoped<ITransactionHandler, EscrowConfirmedHandler>(TransactionTypes.Escrow);
        services.AddKeyedScoped<ITransactionHandler, VerifyTransactionHandler>(TransactionTypes.Verify);
        services.AddKeyedScoped<IPaymentFailureHandler, EscrowFailedHandler>(TransactionTypes.Escrow);
        services.AddKeyedScoped<IPaymentFailureHandler, SettlementFailedHandler>(TransactionTypes.Settlement);

        return services;
    }

    public static IServiceCollection AddPaymentDevSeeder(this IServiceCollection services)
    {
        services.AddScoped<IDevSeeder, PaymentDevSeeder>();
        return services;
    }

    public static IServiceCollection AddPaymentTestSeeder(this IServiceCollection services)
    {
        services.AddScoped<ITestSeeder, PaymentTestSeeder>();
        return services;
    }
}
