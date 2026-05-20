namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeValidator
{
    Task<bool> ValidateAccountAsync();
    Task<bool> ValidateCustomerAsync();
}
