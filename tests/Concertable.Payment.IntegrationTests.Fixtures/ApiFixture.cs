using Concertable.Kernel;
using Concertable.Kernel.DependencyInjection;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Testing.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Concertable.Payment.IntegrationTests.Fixtures;

public sealed class ApiFixture : IAsyncLifetime
{
    private SqlFixture sqlFixture = null!;
    private WebApplicationFactory<Program> factory = null!;

    public IServiceProvider Services => factory.Services;

    public async Task InitializeAsync()
    {
        sqlFixture = new SqlFixture();
        await sqlFixture.InitializeAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Integration);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PaymentDb"] = sqlFixture.ConnectionString,
                    ["ExternalServices:UseRealStripe"] = "false",
                    ["PlatformFee:Fee"] = "0"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAzureServiceBus();
                services.RemoveAll<IStripeSessionClient>();
                services.AddSingleton<ControllableStripeSessionClient>();
                services.AddSingleton<IStripeSessionClient>(provider =>
                    provider.GetRequiredService<ControllableStripeSessionClient>());
            });
        });

        _ = factory.Services;
        await sqlFixture.InitializeRespawnerAsync();
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await sqlFixture.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await sqlFixture.ResetAsync();
        Services.GetRequiredService<ControllableStripeSessionClient>().Reset();
    }

    public Task RunAsync<T>(Func<T, Task> action)
        where T : notnull =>
        Services.GetRequiredService<IScoped<T>>().RunAsync(action);

    public Task<TResult> RunAsync<T, TResult>(Func<T, Task<TResult>> action)
        where T : notnull =>
        Services.GetRequiredService<IScoped<T>>().RunAsync(action);

    public void SetProviderRetrievalUnavailable(bool unavailable) =>
        Services.GetRequiredService<ControllableStripeSessionClient>()
            .SetRetrievalUnavailable(unavailable);

    public void SetProviderStatus(
        string providerObjectId,
        string status,
        DateTimeOffset? captureBefore = null) =>
        Services.GetRequiredService<ControllableStripeSessionClient>()
            .SetStatus(providerObjectId, status, captureBefore);
}
