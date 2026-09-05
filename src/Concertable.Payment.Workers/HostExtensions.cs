using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Kernel;
using Concertable.Messaging.Infrastructure.Inbox;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Contracts.Events;
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
                    .SubscribeTo<PaymentMethodOwnerRegisteredEvent>()
                    .SubscribeTo<PayoutOwnerRegisteredEvent>()
                    .SubscribeTo<PaymentSucceededEvent>()
                    .SubscribeTo<PaymentFailedEvent>());

            services.AddOutbox(
                opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")),
                runDispatcher: false);
            services.AddInbox(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

            return builder;
        }
    }

    extension(IHost app)
    {
        public async Task MigrateStoresAsync()
        {
            await app.Services.MigratePaymentDatabaseAsync();

            using var scope = app.Services.CreateScope();
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<OutboxDbContext>().Database.MigrateAsync();
            await sp.GetRequiredService<InboxDbContext>().Database.MigrateAsync();
        }
    }
}
