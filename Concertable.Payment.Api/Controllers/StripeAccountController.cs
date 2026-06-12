using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Concertable.Payment.Api.Identity;

namespace Concertable.Payment.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
internal sealed class StripeAccountController : ControllerBase
{
    private readonly IStripeAccountClient stripeAccountClient;
    private readonly ICurrentPayoutOwner currentPayoutOwner;
    private readonly IPayoutAccountRepository payoutAccountRepository;

    public StripeAccountController(
        IStripeAccountClient stripeAccountClient,
        ICurrentPayoutOwner currentPayoutOwner,
        IPayoutAccountRepository payoutAccountRepository)
    {
        this.stripeAccountClient = stripeAccountClient;
        this.currentPayoutOwner = currentPayoutOwner;
        this.payoutAccountRepository = payoutAccountRepository;
    }

    [HttpGet("onboarding-link")]
    public async Task<ActionResult<string>> GetOnboardingLink()
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(currentPayoutOwner.OwnerId);
        if (account?.StripeAccountId is null) return BadRequest("No Stripe connect account found.");

        return Ok(await stripeAccountClient.GetOnboardingLinkAsync(account.StripeAccountId));
    }

    [HttpGet("account-status")]
    public async Task<ActionResult<PayoutAccountStatus>> GetAccountStatus()
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(currentPayoutOwner.OwnerId);
        if (account?.StripeAccountId is null) return Ok(PayoutAccountStatus.NotVerified);

        return Ok(await stripeAccountClient.GetAccountStatusAsync(account.StripeAccountId));
    }

    [HttpGet("payment-method")]
    public async Task<ActionResult<PaymentMethodDto?>> GetPaymentMethod()
    {
        var account = await payoutAccountRepository.GetByOwnerIdAsync(currentPayoutOwner.OwnerId);
        if (account?.StripeCustomerId is null) return Ok(null);

        return Ok(await stripeAccountClient.GetPaymentMethodDetailsAsync(account.StripeCustomerId));
    }

    [HttpPost("setup-intent")]
    public async Task<ActionResult<string>> CreateSetupIntent()
    {
        var ownerId = currentPayoutOwner.OwnerId;
        var account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId);

        if (account is null) return Unauthorized();

        var stripeCustomerId = account.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            await stripeAccountClient.ProvisionCustomerAsync(ownerId, account.Email);
            account = await payoutAccountRepository.GetByOwnerIdAsync(ownerId);
            stripeCustomerId = account?.StripeCustomerId
                ?? throw new InvalidOperationException("Failed to provision Stripe customer.");
        }

        return Ok(await stripeAccountClient.CreateSetupIntentAsync(stripeCustomerId));
    }
}
