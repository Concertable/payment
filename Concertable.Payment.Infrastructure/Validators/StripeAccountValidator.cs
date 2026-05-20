namespace Concertable.Payment.Infrastructure.Validators;

internal class StripeAccountValidator : IStripeValidationStrategy
{
    private readonly ICurrentUser currentUser;
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public StripeAccountValidator(ICurrentUser currentUser, IStripeAccountClient stripeAccountClient, IPayoutAccountRepository payoutAccountRepository)
    {
        this.currentUser = currentUser;
        this.stripeAccountClient = stripeAccountClient;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    public async Task<bool> ValidateAsync()
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(currentUser.GetId());
        if (account?.StripeAccountId is null) return false;

        return await stripeAccountClient.GetAccountStatusAsync(account.StripeAccountId) == PayoutAccountStatus.Verified;
    }
}
