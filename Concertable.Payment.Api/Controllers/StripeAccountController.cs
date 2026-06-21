using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Concertable.Payment.Api.Identity;

namespace Concertable.Payment.Api.Controllers;

/* Serves Customer card-management directly (owner = the buyer's own id, read from the `owner` claim).
   B2B managers no longer reach these endpoints — B2B fronts the same operations over the PayoutAccount
   gRPC service, passing the tenant id as owner (see Concertable.B2B.Tenant.Api.StripeAccountController). */
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
    public async Task<ActionResult<string>> CreateSetupIntent() =>
        await payoutAccountService.CreateSetupIntentAsync(currentPayoutOwner.OwnerId) is { } secret
            ? Ok(secret)
            : Unauthorized();
}
