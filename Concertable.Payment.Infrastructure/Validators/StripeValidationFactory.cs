using Microsoft.Extensions.DependencyInjection;

namespace Concertable.Payment.Infrastructure.Validators;

internal class StripeValidationFactory : IStripeValidationFactory
{
    private readonly IKeyedServiceProvider serviceProvider;

    public StripeValidationFactory(IKeyedServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public IStripeValidationStrategy Create(ContractType contractType) =>
        serviceProvider.GetRequiredKeyedService<IStripeValidationStrategy>(contractType);
}
