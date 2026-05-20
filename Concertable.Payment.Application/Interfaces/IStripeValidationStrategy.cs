namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeValidationStrategy : IContractStrategy
{
    Task<bool> ValidateAsync();
}
