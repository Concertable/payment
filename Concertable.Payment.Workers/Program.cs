using Concertable.Authorization.Infrastructure.Extensions;
using Concertable.DataAccess.Infrastructure;
using Concertable.Messaging.Application;
using Concertable.Messaging.AzureServiceBus;
using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Messaging.Infrastructure.Inbox;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Domain.Events;
using Concertable.Payment.Infrastructure.Extensions;
using Concertable.Shared.Infrastructure.Extensions;
using Concertable.User.Contracts.Events;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Configuration.AddEnvironmentVariables();

var services = builder.Services;

services.AddScoped<IKeyedServiceProvider>(sp => (IKeyedServiceProvider)sp);
services.AddSingleton(TimeProvider.System);
services.AddSharedInfrastructure(builder.Configuration);
services.AddScoped<AuditInterceptor>();
services.AddScoped<DomainEventDispatchInterceptor>();
services.AddAuthorizationModule();
services.AddPaymentInfrastructure(builder.Configuration);

services.AddAzureServiceBusTransport(
    opts =>
    {
        opts.ConnectionString = builder.Configuration.GetConnectionString("asb") ?? "";
        opts.ServiceName = "concertable-payment";
    },
    reg => reg
        .SubscribeTo<CustomerRegisteredEvent>()
        .SubscribeTo<VenueManagerRegisteredEvent>()
        .SubscribeTo<ArtistManagerRegisteredEvent>()
        .SubscribeTo<PaymentSucceededEvent>()
        .SubscribeTo<PaymentFailedEvent>());

services.AddDirectBusKeyed("webhook");
services.AddOutbox(
    opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")),
    runDispatcher: false);
services.AddInbox(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

var app = builder.Build();

await app.Services.MigratePaymentDatabaseAsync();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<OutboxDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<InboxDbContext>().Database.MigrateAsync();
}

app.Run();
