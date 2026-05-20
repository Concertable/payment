using Concertable.Payment.Application.Interfaces;
using Concertable.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Concertable.Payment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
internal class TransactionController : ControllerBase
{
    private readonly ITransactionService purchaseService;

    public TransactionController(ITransactionService purchaseService)
    {
        this.purchaseService = purchaseService;
    }

    [HttpGet]
    public async Task<ActionResult<Pagination<ITransaction>>> GetPurchases([FromQuery] PageParams pageParams)
    {
        var result = await purchaseService.GetAsync(pageParams);
        return Ok(result);
    }
}
