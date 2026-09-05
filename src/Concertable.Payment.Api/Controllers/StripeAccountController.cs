using Concertable.Payment.Api.Identity;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Enums;
using Concertable.Payment.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Concertable.Payment.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
internal sealed class StripeAccountController : ControllerBase
{
    private readonly IPayoutAccountService payoutAccountService;
    private readonly ICurrentPayoutOwner currentPayoutOwner;

    public StripeAccountController(IPayoutAccountService payoutAccountService, ICurrentPayoutOwner currentPayoutOwner)
    {
        this.payoutAccountService = payoutAccountService;
        this.currentPayoutOwner = currentPayoutOwner;
    }

    [HttpGet("onboarding-link")]
    public async Task<ActionResult<string>> GetOnboardingLink() =>
        await payoutAccountService.GetOnboardingLinkAsync(currentPayoutOwner.OwnerId) is { } link
            ? Ok(link)
            : BadRequest("No Stripe connect account found.");

    [HttpGet("account-status")]
    public async Task<ActionResult<PayoutAccountStatus>> GetAccountStatus() =>
        Ok(await payoutAccountService.GetAccountStatusAsync(currentPayoutOwner.OwnerId));

    [HttpGet("payment-method")]
    public async Task<ActionResult<PaymentMethodDto?>> GetPaymentMethod() =>
        Ok(await payoutAccountService.GetPaymentMethodAsync(currentPayoutOwner.OwnerId));

    [HttpPost("setup-intent")]
    [EnableRateLimiting(RateLimitPolicies.SetupIntent)]
    public async Task<ActionResult<string>> CreateSetupIntent() =>
        await payoutAccountService.CreateSetupIntentAsync(currentPayoutOwner.OwnerId) is { } secret
            ? Ok(secret)
            : Unauthorized();
}
