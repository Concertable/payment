using Concertable.Payment.Workers;
using Concertable.Testing.Architecture;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Concertable.Payment.StartupTests;

public sealed class WorkerHostTests
{
    [Fact]
    public void ProductionGraphAndStrictValidation_AreValid()
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
}
