using Concertable.Payment.Api.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Concertable.Payment.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddPaymentControllers(this IServiceCollection services)
        => services.AddControllers().AddApplicationPart(typeof(WebhookController).Assembly);
}
