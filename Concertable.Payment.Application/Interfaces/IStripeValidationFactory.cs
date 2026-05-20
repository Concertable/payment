namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeValidationFactory
{
    IStripeValidationStrategy Create(ContractType contractType);
}
