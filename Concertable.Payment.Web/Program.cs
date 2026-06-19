using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Payment.Application.Commands;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Api.Extensions;
using Concertable.Payment.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Grpc;
using Concertable.Payment.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Concertable.ServiceDefaults;
using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Messaging.Application.Extensions;
using Concertable.Messaging.AzureServiceBus.Extensions;
using Concertable.Kernel.Extensions;
using Concertable.Seed.Shared.Extensions;
using Concertable.Payment.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Configuration.AddEnvironmentVariables();

builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ConfigureEndpointDefaults(e => e.Protocols = HttpProtocols.Http1AndHttp2);
});

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithOrigins(corsOrigins);
    });
});

var services = builder.Services;

services.AddScoped<IKeyedServiceProvider>(sp => (IKeyedServiceProvider)sp);
services.AddSingleton(TimeProvider.System);
services.AddSharedInfrastructure(builder.Configuration);
services.AddQueueHostedService();
services.AddScoped<AuditInterceptor>();
services.AddScoped<IDomainEventDispatchInterceptor, DomainEventDispatchInterceptor>();
services.AddSeedingInfrastructure();
services.AddCurrentUser();
services.AddPaymentInfrastructure(builder.Configuration);

if (builder.Environment.EnvironmentName == "E2E")
    services.UseE2EStripeClient();

services.AddScoped<GrpcExceptionInterceptor>();
services.AddGrpc(options => options.Interceptors.Add<GrpcExceptionInterceptor>());
services.AddPaymentControllers();

services.AddAzureServiceBusTransport(
    opts =>
    {
        opts.ConnectionString = builder.Configuration.GetConnectionString("asb") ?? "";
        opts.ServiceName = "concertable-payment";
    },
    reg =>
    {
        reg.Publishes<PaymentSucceededEvent>();
        reg.Publishes<PaymentFailedEvent>();
        reg.HandleCommand<ProcessStripeWebhookCommand>();
    });
services.AddOutbox(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false;
        opts.Authority = builder.Configuration["Auth:Authority"] ?? builder.Configuration["services__auth__https__0"];
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero,
            ValidateIssuer = !builder.Environment.IsDevelopment(),
            ValidAudiences = ["concertable.payment.api", "concertable.b2b.api", "concertable.customer.api"]
        };
    });

services.AddAuthorization(opts =>
{
    opts.AddPolicy("ServiceToken", p => p.RequireClaim("scope", "payment:write"));
});

services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapPaymentGrpcServices();
app.MapControllers();
app.MapDefaultEndpoints();

if (!app.Environment.IsProduction())
    await app.Services.MigratePaymentDatabaseAsync();

app.Run();

public sealed partial class Program
{ }
