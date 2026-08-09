using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Concertable.Payment.Seed;

public static class E2EServiceCollectionExtensions
{
    public static IServiceCollection UseE2EStripeClient(this IServiceCollection services)
    {
        services.AddSingleton<StripeE2EAccountResolver>();
        services.RemoveAll<IStripeEventFilter>();
        services.AddSingleton<IStripeEventFilter>(sp => sp.GetRequiredService<StripeE2EAccountResolver>());
        services.RemoveAll<IStripeAccountClient>();
        services.AddScoped<IStripeAccountClient, E2EStripeAccountClient>();
        return services;
    }
}
