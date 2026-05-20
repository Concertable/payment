namespace Concertable.Payment.Infrastructure.Validators;

internal class StripeValidator : IStripeValidator
{
    private readonly StripeAccountValidator accountValidator;
    private readonly StripeCustomerValidator customerValidator;

    public StripeValidator(StripeAccountValidator accountValidator, StripeCustomerValidator customerValidator)
    {
        this.accountValidator = accountValidator;
        this.customerValidator = customerValidator;
    }

    public Task<bool> ValidateAccountAsync() => accountValidator.ValidateAsync();
    public Task<bool> ValidateCustomerAsync() => customerValidator.ValidateAsync();
}
