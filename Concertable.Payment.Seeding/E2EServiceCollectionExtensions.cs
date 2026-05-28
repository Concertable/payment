using Concertable.Payment.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Concertable.Payment.Seeding;

public static class E2EServiceCollectionExtensions
{
    public static IServiceCollection UseE2EStripeClient(this IServiceCollection services)
    {
        services.AddSingleton<StripeE2EAccountResolver>();
        services.RemoveAll<IStripeAccountClient>();
        services.AddScoped<IStripeAccountClient, E2EStripeAccountClient>();
        return services;
    }
}
