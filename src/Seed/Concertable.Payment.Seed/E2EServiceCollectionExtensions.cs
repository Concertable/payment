using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Seed;

public static class E2EServiceCollectionExtensions
{
    public static IServiceCollection UseE2EStripeClient(this IServiceCollection services)
    {
        services.AddSingleton<StripeE2EAccountResolver>();
        services.RemoveAll<IStripeAccountClient>();
        services.AddScoped<IStripeAccountClient, E2EStripeAccountClient>();
        services.RemoveAll<IWebhookProcessor>();
        services.AddScoped<IWebhookProcessor>(sp => new E2EStripeWebhookProcessor(
            ActivatorUtilities.CreateInstance<WebhookProcessor>(sp),
            sp.GetRequiredService<StripeE2EAccountResolver>(),
            sp.GetRequiredService<ILogger<E2EStripeWebhookProcessor>>()));
        return services;
    }
}
