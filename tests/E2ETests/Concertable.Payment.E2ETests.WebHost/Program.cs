using Concertable.Payment.E2ETests.Stripe;
using Concertable.Payment.Web;

namespace Concertable.Payment.E2ETests.WebHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddWebHost();
        builder.Services.UseStripeAdapter();

        var app = builder.Build();

        await app.UseWebHost();

        app.Run();
    }
}
