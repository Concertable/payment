using Concertable.Composition.Testing;
using Concertable.Payment.Web;
using Concertable.Payment.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentArchitectureTests
{
    [Fact]
    public void Web_ProductionGraphAndStrictValidation_AreValid()
    {
        var builder = WebApplication.CreateBuilder(CompositionTestArguments.Create());
        builder.AddWebHost();
        using var app = builder.Build();
        builder.Services.ValidateComposition(app.Services, new CompositionValidationOptions
        {
            RootAssemblies = [typeof(Concertable.Payment.Web.HostExtensions).Assembly]
        });
        var invalidBuilder = WebApplication.CreateBuilder(CompositionTestArguments.Create());
        invalidBuilder.AddWebHost();
        invalidBuilder.Services.AddInvalidLifetimeGraph();
        Assert.ThrowsAny<Exception>(() => invalidBuilder.Build());
    }

    [Fact]
    public void Workers_ProductionGraphAndStrictValidation_AreValid()
    {
        var builder = Host.CreateApplicationBuilder(CompositionTestArguments.Create());
        builder.AddWorkerHost();
        using var app = builder.Build();
        builder.Services.ValidateComposition(app.Services, new CompositionValidationOptions
        {
            RootAssemblies = [typeof(Concertable.Payment.Workers.HostExtensions).Assembly]
        });
        var invalidBuilder = Host.CreateApplicationBuilder(CompositionTestArguments.Create());
        invalidBuilder.AddWorkerHost();
        invalidBuilder.Services.AddInvalidLifetimeGraph();
        Assert.ThrowsAny<Exception>(() => invalidBuilder.Build());
    }

    [Fact]
    public void AppHost_ProductionGraphAndStrictValidation_AreValid()
    {
        using var app = PaymentAppHost.CreateBuilder([]).Build();
        var builder = PaymentAppHost.CreateBuilder([]);
        builder.Services.AddInvalidLifetimeGraph();
        Assert.ThrowsAny<Exception>(() => builder.Build());
    }
}
