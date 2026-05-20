using Concertable.Authorization.Contracts;
using Concertable.User.Contracts;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Concertable.Payment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
internal class StripeAccountController : ControllerBase
{
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly ICurrentUser currentUser;
    private readonly IUserModule userModule;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public StripeAccountController(
        IStripeAccountClient stripeAccountClient,
        ICurrentUser currentUser,
        IUserModule userModule,
        IPayoutAccountRepository payoutAccountRepository)
    {
        this.stripeAccountClient = stripeAccountClient;
        this.currentUser = currentUser;
        this.userModule = userModule;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    [HttpGet("onboarding-link")]
    public async Task<ActionResult<string>> GetOnboardingLink()
    {
        _ = await userModule.GetManagerByIdAsync(currentUser.GetId())
            ?? throw new UnauthorizedAccessException("Manager not found.");

        var account = await payoutAccountRepository.GetByUserIdAsync(currentUser.GetId());
        if (account?.StripeAccountId is null) return BadRequest("No Stripe connect account found.");

        return Ok(await stripeAccountClient.GetOnboardingLinkAsync(account.StripeAccountId));
    }

    [HttpGet("account-status")]
    public async Task<ActionResult<PayoutAccountStatus>> GetAccountStatus()
    {
        _ = await userModule.GetManagerByIdAsync(currentUser.GetId())
            ?? throw new UnauthorizedAccessException("Manager not found.");

        var account = await payoutAccountRepository.GetByUserIdAsync(currentUser.GetId());
        if (account?.StripeAccountId is null) return Ok(PayoutAccountStatus.NotVerified);

        return Ok(await stripeAccountClient.GetAccountStatusAsync(account.StripeAccountId));
    }

    [HttpGet("payment-method")]
    public async Task<ActionResult<PaymentMethodDto?>> GetPaymentMethod()
    {
        var account = await payoutAccountRepository.GetByUserIdAsync(currentUser.GetId());
        if (account?.StripeCustomerId is null) return Ok(null);

        return Ok(await stripeAccountClient.GetPaymentMethodDetailsAsync(account.StripeCustomerId));
    }

    [HttpPost("setup-intent")]
    public async Task<ActionResult<string>> CreateSetupIntent()
    {
        if (currentUser.Id is null)
            return Ok(await stripeAccountClient.CreateSetupIntentAsync(null));

        var userId = currentUser.GetId();
        var account = await payoutAccountRepository.GetByUserIdAsync(userId);
        var stripeCustomerId = account?.StripeCustomerId;

        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            var user = await userModule.GetByIdAsync(userId);
            if (user is null) return Unauthorized();
            await stripeAccountClient.ProvisionCustomerAsync(userId, user.Email);
            account = await payoutAccountRepository.GetByUserIdAsync(userId);
            stripeCustomerId = account?.StripeCustomerId
                ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
        }

        return Ok(await stripeAccountClient.CreateSetupIntentAsync(stripeCustomerId));
    }
}
