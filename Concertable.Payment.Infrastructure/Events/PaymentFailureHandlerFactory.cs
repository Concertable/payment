using Microsoft.Extensions.DependencyInjection;

namespace Concertable.Payment.Infrastructure.Events;

internal class PaymentFailureHandlerFactory : IPaymentFailureHandlerFactory
{
    private readonly IKeyedServiceProvider serviceProvider;

    public PaymentFailureHandlerFactory(IKeyedServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IPaymentFailureHandler? Create(string type) =>
        serviceProvider.GetKeyedService<IPaymentFailureHandler>(type);
}
