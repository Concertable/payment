using Concertable.Payment.Application.Interfaces.Webhook;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Concertable.Payment.Api.Controllers;

[Route("api/[controller]")]
internal class WebhookController : ControllerBase
{
    private readonly IWebhookService webhookService;
    private readonly ILogger<WebhookController> logger;

    public WebhookController(IWebhookService webhookService, ILogger<WebhookController> logger)
    {
        this.webhookService = webhookService;
        this.logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        logger.LogInformation("[WebhookController] webhook received bytes={Bytes}", json.Length);

        try
        {
            await webhookService.HandleAsync(json, Request.Headers["Stripe-Signature"]!);
        }
        catch (StripeException)
        {
            return Problem("Webhook validation failed");
        }

        return Ok();
    }
}
