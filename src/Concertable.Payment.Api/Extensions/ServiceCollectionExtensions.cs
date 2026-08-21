using Concertable.Payment.Api.Controllers;
using Concertable.Payment.Api.Identity;
using Concertable.Shared.Api.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Concertable.Payment.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddPaymentControllers(this IServiceCollection services)
    {
        services.AddScoped<ICurrentPayoutOwner, CurrentPayoutOwner>();
        return services.AddControllers()
            .AddApplicationJson()
            .AddInternalControllers(typeof(WebhookController).Assembly);
    }
}
