using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Services;
using Shared.Exchange;

namespace Server.Controllers;

[ApiController]
[Authorize]
[Route("api/exchange")]
public sealed class ExchangeController : ControllerBase
{
    private readonly IExchangeRateService _service;

    public ExchangeController(IExchangeRateService service)
    {
        _service = service;
    }

    [HttpGet("rates")]
    public async Task<ActionResult<List<ExchangeRateDto>>> GetRates(CancellationToken ct)
    {
        var rates = await _service.GetRatesAsync(ct);
        return Ok(rates);
    }

    [HttpGet("convert")]
    public async Task<ActionResult<decimal>> Convert(string from, string to, decimal amount, CancellationToken ct)
    {
        var result = await _service.ConvertAsync(from, to, amount, ct);
        if (result is null) return BadRequest("Unable to convert. Check currency codes.");
        
        return Ok(result);
    }
}
