using Microsoft.Extensions.DependencyInjection;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripePaymentIntentClientFactory : IStripePaymentIntentClientFactory
{
    private readonly IServiceProvider serviceProvider;

    public StripePaymentIntentClientFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IStripePaymentIntentClient Create(PaymentSession session)
        => serviceProvider.GetRequiredKeyedService<IStripePaymentIntentClient>(session);
}
