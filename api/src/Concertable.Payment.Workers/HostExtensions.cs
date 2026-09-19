using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Kernel;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Concertable.ServiceDefaults;
using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Messaging.Application.Extensions;
using Concertable.Messaging.AzureServiceBus.Extensions;
using Concertable.Kernel.Extensions;
using Concertable.Seed.Shared.Extensions;

namespace Concertable.Payment.Workers;

public static class HostExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder AddWorkerHost()
        {
            builder.AddServiceDefaults();
            builder.Configuration.AddEnvironmentVariables();

            var services = builder.Services;

            services.AddScoped<IKeyedServiceProvider>(sp => (IKeyedServiceProvider)sp);
            services.AddSingleton(TimeProvider.System);
            services.AddSharedInfrastructure(builder.Configuration);
            services.AddScoped<AuditInterceptor>();
            services.AddScoped<IDomainEventDispatchInterceptor, DomainEventDispatchInterceptor>();
            services.AddSeedingInfrastructure();
            services.AddCurrentUser();
            services.AddPaymentInfrastructure(builder.Configuration);

            services.AddAzureServiceBusTransport(
                opts =>
                {
                    opts.ConnectionString = builder.Configuration.GetConnectionString("asb")
                        ?? (builder.Environment.IsIntegration() ? null!
                            : throw new InvalidOperationException("Connection string 'asb' is required."));
                    opts.ServiceName = builder.Configuration["ServiceBus:ServiceName"]
                        ?? (builder.Environment.IsIntegration() ? "concertable-payment"
                            : throw new InvalidOperationException("Configuration 'ServiceBus:ServiceName' is required."));
                },
                reg => reg
                    .Publishes<PaymentOperationStateChanged>()
                    .SubscribeTo<PaymentMethodOwnerRegisteredEvent>()
                    .SubscribeTo<PayoutOwnerRegisteredEvent>()
                    .SubscribeTo<PaymentSucceededEvent>()
                    .SubscribeTo<PaymentFailedEvent>());

            services.AddOutbox(
                opt => opt.UseNpgsql(
                    builder.Configuration.GetConnectionString("PaymentDb"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Outbox", Schema.Messaging)),
                runDispatcher: false);
            services.AddInbox(opt => opt.UseNpgsql(
                builder.Configuration.GetConnectionString("PaymentDb"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Inbox", Schema.Messaging)));

            return builder;
        }
    }
}
