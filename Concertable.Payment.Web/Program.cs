using Concertable.Authorization.Infrastructure.Extensions;
using Concertable.DataAccess.Infrastructure;
using Concertable.Messaging.AzureServiceBus;
using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Payment.Api.Extensions;
using Concertable.Payment.Infrastructure.Extensions;
using Concertable.Shared.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Configuration.AddEnvironmentVariables();

builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ConfigureEndpointDefaults(e => e.Protocols = HttpProtocols.Http1AndHttp2);
});

var services = builder.Services;

services.AddScoped<IKeyedServiceProvider>(sp => (IKeyedServiceProvider)sp);
services.AddSingleton(TimeProvider.System);
services.AddSharedInfrastructure(builder.Configuration);
services.AddScoped<AuditInterceptor>();
services.AddScoped<DomainEventDispatchInterceptor>();
services.AddAuthorizationModule();
services.AddPaymentInfrastructure(builder.Configuration);

services.AddGrpc();
services.AddPaymentControllers();

services.AddAzureServiceBusTransport(
    opts =>
    {
        opts.ConnectionString = builder.Configuration.GetConnectionString("asb") ?? "";
        opts.ServiceName = "concertable-payment";
    },
    _ => { });
services.AddDirectBusKeyed("webhook");
services.AddOutbox(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false;
        opts.Authority = builder.Configuration["Auth:Authority"] ?? builder.Configuration["services__auth__https__0"];
        opts.Audience = "concertable.payment.api";
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero,
            ValidateIssuer = !builder.Environment.IsDevelopment()
        };
    });

services.AddAuthorization(opts =>
{
    opts.AddPolicy("ServiceToken", p => p.RequireClaim("scope", "payment:write"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapPaymentGrpcServices();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

public partial class Program { }
