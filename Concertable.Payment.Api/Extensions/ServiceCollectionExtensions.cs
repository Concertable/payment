using Concertable.Payment.Api.Controllers;
using Concertable.Shared.Api.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Concertable.Payment.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddPaymentControllers(this IServiceCollection services)
        => services.AddControllers()
            .AddInternalControllers(typeof(WebhookController).Assembly);
}
