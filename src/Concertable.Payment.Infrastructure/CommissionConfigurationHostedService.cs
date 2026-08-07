using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Concertable.Payment.Infrastructure;

internal sealed class CommissionConfigurationHostedService : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;

    public CommissionConfigurationHostedService(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<CommissionConfigurationInitializer>()
            .InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
