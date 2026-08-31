using Concertable.Payment.E2ETests.Server;
using Concertable.Payment.E2ETests.Stripe;
using Concertable.Payment.Web;

namespace Concertable.Payment.E2ETests.Web;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Content root = output dir so the linked appsettings.E2E.json (copied there, not the project
        // dir Aspire uses as content root) loads — else UseRealStripe stays false and the Stripe adapter fails DI validation.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.AddWebHost();
        builder.Services.AddPaymentE2EAdmin(builder.Configuration, builder.Environment);
        builder.Services.UseStripeAdapter();

        var app = builder.Build();

        app.MapPaymentE2EAdmin();
        await app.UseWebHost();

        app.Run();
    }
}
