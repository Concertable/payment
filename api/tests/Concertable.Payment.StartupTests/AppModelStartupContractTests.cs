using Concertable.Payment.Hosting;
using Concertable.Payment.Web;
using Concertable.Payment.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Concertable.Payment.StartupTests;

public sealed class AppModelStartupContractTests
{
    [Fact]
    public async Task WebHost_StartsOnTheConfigurationTheAppModelSupplies()
    {
        // Empty Stripe key on purpose: a configured one makes the AppHost add the Stripe CLI, whose
        // environment callback blocks 60s awaiting a log line from a process that is not running here.
        var appModel = AppHost.CreateBuilder(["--Stripe:SecretKey="]);
        var arguments = await AppModelConfiguration.ArgumentsForAsync(
            appModel, PaymentConstants.WebResource, AppModelConfiguration.Secrets);

        var builder = WebApplication.CreateBuilder(arguments);
        builder.AddWebHost();
        using var app = builder.Build();

        app.Services.GetService<IStartupValidator>()?.Validate();
    }

    [Fact]
    public async Task WorkerHost_StartsOnTheConfigurationTheAppModelSupplies()
    {
        // Empty Stripe key on purpose: a configured one makes the AppHost add the Stripe CLI, whose
        // environment callback blocks 60s awaiting a log line from a process that is not running here.
        var appModel = AppHost.CreateBuilder(["--Stripe:SecretKey="]);
        var arguments = await AppModelConfiguration.ArgumentsForAsync(
            appModel, PaymentConstants.WorkersResource, AppModelConfiguration.Secrets);

        var builder = Host.CreateApplicationBuilder(arguments);
        builder.AddWorkerHost();
        using var app = builder.Build();

        app.Services.GetService<IStartupValidator>()?.Validate();
    }
}
