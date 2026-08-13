using Concertable.Payment.E2ETests.Stripe;
using Concertable.Payment.Workers;

namespace Concertable.Payment.E2ETests.WorkersHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.AddWorkerHost();
        builder.Services.UseStripeAdapter();

        var app = builder.Build();

        await app.MigrateStoresAsync();

        app.Run();
    }
}
