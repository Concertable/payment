using Concertable.Payment.E2ETests.Stripe;
using Concertable.Payment.Workers;

namespace Concertable.Payment.E2ETests.Workers;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Content root = output dir so the linked appsettings.E2E.json (copied there, not the project
        // dir Aspire uses as content root) loads — else UseRealStripe stays false and the Stripe adapter fails DI validation.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.AddWorkerHost();
        builder.Services.UseStripeAdapter();

        var app = builder.Build();

        await app.MigrateStoresAsync();

        app.Run();
    }
}
