using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.E2ETests.Stripe;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Swaps the production Stripe account client and webhook processor for the E2E test-mode
    /// variants that bind pre-provisioned Connect accounts and run-scoped customers. Called only by
    /// the Payment E2E host projects, after the ordinary Payment host composition.
    /// </summary>
    public static IServiceCollection UseStripeAdapter(this IServiceCollection services)
    {
        services.AddSingleton<StripeAccountResolver>();
        services.RemoveAll<IStripeAccountClient>();
        services.AddScoped<IStripeAccountClient, StripeAccountClient>();
        services.RemoveAll<IWebhookProcessor>();
        services.AddScoped<IWebhookProcessor>(sp => new StripeWebhookProcessor(
            ActivatorUtilities.CreateInstance<WebhookProcessor>(sp),
            sp.GetRequiredService<StripeAccountResolver>(),
            sp.GetRequiredService<ILogger<StripeWebhookProcessor>>()));
        return services;
    }
}
