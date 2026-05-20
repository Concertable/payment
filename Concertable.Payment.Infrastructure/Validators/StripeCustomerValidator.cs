namespace Concertable.Payment.Infrastructure.Validators;

internal class StripeCustomerValidator : IStripeValidationStrategy
{
    private readonly ICurrentUser currentUser;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public StripeCustomerValidator(ICurrentUser currentUser, IPayoutAccountRepository payoutAccountRepository)
    {
        this.currentUser = currentUser;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task<bool> ValidateAsync()
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(currentUser.GetId());
        return !string.IsNullOrEmpty(account?.StripeCustomerId);
    }
}
