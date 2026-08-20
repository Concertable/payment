using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Kernel;
using Concertable.Payment.Application.Commands;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Contracts;
using Concertable.Payment.Api;
using Concertable.Payment.Api.Extensions;
using Concertable.Payment.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Grpc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Concertable.ServiceDefaults;
using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Messaging.Application.Extensions;
using Concertable.Messaging.AzureServiceBus.Extensions;
using Concertable.Kernel.Extensions;
using Concertable.Seed.Shared.Extensions;
using Concertable.Shared.Api.Exceptions;

namespace Concertable.Payment.Web;

public static class HostExtensions
{
    public static WebApplicationBuilder AddWebHost(this WebApplicationBuilder builder)
    {
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

        services.AddScoped<GrpcExceptionInterceptor>();
        services.AddGrpc(options => options.Interceptors.Add<GrpcExceptionInterceptor>());
        services.AddProblemDetails();
        services.AddPaymentControllers();

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
            reg =>
            {
                reg.Publishes<PaymentSucceededEvent>();
                reg.Publishes<PaymentFailedEvent>();
                reg.Publishes<CaptureEscrowSucceededEvent>();
                reg.Publishes<CaptureEscrowRejectedEvent>();
                reg.Publishes<DepositEscrowSucceededEvent>();
                reg.Publishes<DepositEscrowRejectedEvent>();
                reg.Publishes<RefundEscrowSucceededEvent>();
                reg.Publishes<RefundEscrowRejectedEvent>();
                reg.Publishes<RefundEscrowDeferredEvent>();
                reg.HandleCommand<ProcessStripeWebhookCommand>();
                reg.HandleCommand<CaptureEscrowCommand>();
                reg.HandleCommand<DepositEscrowCommand>();
                reg.HandleCommand<RefundEscrowCommand>();
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

        services.Configure<ForwardedHeadersOptions>(options =>
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        builder.AddDefaultRateLimiting();
        builder.AddRateLimitPolicy(RateLimitPolicies.SetupIntent, new RateLimitWindow { PermitLimit = 10, WindowSeconds = 60 }, perUser: true);

        return builder;
    }

    public static async Task UseWebHost(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultRateLimiting();

        app.MapPaymentGrpcServices();
        app.MapControllers();
        app.MapDefaultEndpoints();

        if (!app.Environment.IsProduction())
            await app.Services.MigratePaymentDatabaseAsync();
    }
}
