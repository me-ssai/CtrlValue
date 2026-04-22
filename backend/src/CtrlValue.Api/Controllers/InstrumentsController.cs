using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InstrumentsController : ControllerBase
{
    private readonly IInstrumentService _instrumentService;
    private readonly AppDbContext _db;
    private readonly AlphaVantageService _alphaVantage;
    private readonly CoinGeckoService _coinGecko;
    private readonly YahooFinanceService _yahooFinance;
    private readonly MetalsPriceService _metalsPriceService;
    private readonly IEntityIntegrationService _integrationService;

    public InstrumentsController(
        IInstrumentService instrumentService,
        AppDbContext db,
        AlphaVantageService alphaVantage,
        CoinGeckoService coinGecko,
        YahooFinanceService yahooFinance,
        MetalsPriceService metalsPriceService,
        IEntityIntegrationService integrationService)
    {
        _instrumentService  = instrumentService;
        _db                 = db;
        _alphaVantage       = alphaVantage;
        _coinGecko          = coinGecko;
        _yahooFinance       = yahooFinance;
        _metalsPriceService = metalsPriceService;
        _integrationService = integrationService;
    }

    /// <summary>
    /// Returns the curated set of default instruments (IsDefault = true).
    /// </summary>
    [HttpGet("defaults")]
    public async Task<ActionResult<List<InstrumentDto>>> GetDefaultInstruments()
    {
        var instruments = await _db.Instruments
            .Where(i => i.IsDefault && !i.IsDeleted)
            .OrderBy(i => i.InstrumentType)
            .ThenBy(i => i.Symbol)
            .ToListAsync();

        var dtos = instruments.Select(i => new InstrumentDto
        {
            Id             = i.Id,
            Symbol         = i.Symbol,
            Name           = i.Name,
            InstrumentType = i.InstrumentType.ToString(),
            Currency       = i.Currency,
            Exchange       = i.Exchange,
            ExternalSymbol = i.ExternalSymbol,
            PriceProvider  = i.PriceProvider?.ToString(),
            PriceUnit      = i.PriceUnit.ToString(),
            CreatedAt      = i.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Searches for instruments by query string and type.
    /// Checks local DB first, then calls the relevant provider API.
    /// Metals always return the static list (no external call needed).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<InstrumentSearchResultDto>>> SearchInstruments(
        [FromQuery] string query,
        [FromQuery] string? type = null,
        [FromQuery] string? exchange = null)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return Ok(new List<InstrumentSearchResultDto>());

        InstrumentType? instrumentType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<InstrumentType>(type, true, out var parsedType))
            instrumentType = parsedType;

        // ── Check local DB first ──────────────────────────────────────────────
        var localMatches = await _db.Instruments
            .Where(i => !i.IsDeleted
                     && (instrumentType == null || i.InstrumentType == instrumentType)
                     && (i.Symbol.ToLower().Contains(query.ToLower()) || i.Name.ToLower().Contains(query.ToLower())))
            .Take(10)
            .ToListAsync();

        var trackedSymbols = localMatches.Select(i => i.Symbol).ToHashSet();

        var results = localMatches.Select(i => new InstrumentSearchResultDto(
            i.Symbol, i.Name, i.InstrumentType.ToString(),
            i.Exchange, i.Currency, IsAlreadyTracked: true)).ToList();

        // ── Metals: static list ───────────────────────────────────────────────
        if (instrumentType == InstrumentType.METAL)
        {
            var metalList = new[]
            {
                new { Symbol = "XAU", Name = "Gold" },
                new { Symbol = "XAG", Name = "Silver" },
                new { Symbol = "XPT", Name = "Platinum" },
                new { Symbol = "XPD", Name = "Palladium" },
                new { Symbol = "XCU", Name = "Copper" },
            };
            foreach (var m in metalList.Where(m =>
                !trackedSymbols.Contains(m.Symbol) &&
                (m.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Contains(query, StringComparison.OrdinalIgnoreCase))))
            {
                results.Add(new InstrumentSearchResultDto(m.Symbol, m.Name, "METAL", null, "USD", false));
            }
            return Ok(results);
        }

        // If local DB had enough results, return them without calling external APIs
        if (results.Count >= 5) return Ok(results);

        // ── Yahoo Finance: ASX (exclusive) or Global/NYSE (combined with AV) ─────
        bool isAsx    = exchange?.Equals("ASX",    StringComparison.OrdinalIgnoreCase) == true;
        bool isGlobal = exchange?.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase) == true
                     || exchange?.Equals("NYSE",   StringComparison.OrdinalIgnoreCase) == true;

        if ((isAsx || isGlobal) && (instrumentType is null or InstrumentType.STOCK or InstrumentType.ETF))
        {
            try
            {
                // For ASX: append .AX suffix so Yahoo returns the specific ASX-listed instrument
                // (e.g. "GOLD" → searches "GOLD.AX" → returns ETFS Physical Gold).
                // Yahoo's region=AU only sets locale — it does NOT filter to ASX symbols.
                var yfQuery = isAsx
                    ? (query.EndsWith(".AX", StringComparison.OrdinalIgnoreCase) ? query : query + ".AX")
                    : query;

                var region    = isAsx ? "AU" : null;
                var yfResults = await _yahooFinance.SearchAsync(yfQuery, region);

                // For ASX: keep only .AX symbols to block any global results that sneak through
                if (isAsx)
                    yfResults = yfResults.Where(r => r.Symbol.EndsWith(".AX", StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var r in yfResults.Where(r => !trackedSymbols.Contains(r.Symbol)))
                {
                    var itype = r.Type == "ETF" ? "ETF" : "STOCK";
                    if (instrumentType != null && itype != instrumentType.ToString()) continue;
                    results.Add(new InstrumentSearchResultDto(r.Symbol, r.Name, itype, r.Exchange, r.Currency ?? (isAsx ? "AUD" : "USD"), false));
                }

                if (isAsx) return Ok(results.Take(15).ToList()); // ASX-only — no AV fallthrough
                // Global/NYSE: fall through to Alpha Vantage to combine results
            }
            catch
            {
                if (isAsx) return Ok(results.Take(15).ToList()); // still return early for ASX even on failure
                // Global: fall through to Alpha Vantage
            }
        }

        // ── Alpha Vantage: stocks & ETFs ──────────────────────────────────────
        if (instrumentType is null or InstrumentType.STOCK or InstrumentType.ETF)
        {
            var entityId = GetEntityId();
            var apiKey   = entityId.HasValue
                ? await _integrationService.GetEffectiveApiKeyAsync(entityId.Value, "ALPHA_VANTAGE")
                : await _integrationService.GetEffectiveApiKeyAsync(Guid.Empty, "ALPHA_VANTAGE");

            if (apiKey != null)
            {
                var avResults = await _alphaVantage.SearchSymbolAsync(query, apiKey);
                foreach (var r in avResults.Where(r => !trackedSymbols.Contains(r.Symbol)))
                {
                    var itype = r.Type?.Contains("ETF", StringComparison.OrdinalIgnoreCase) == true ? "ETF" : "STOCK";
                    if (instrumentType != null && itype != instrumentType.ToString()) continue;
                    results.Add(new InstrumentSearchResultDto(r.Symbol, r.Name, itype, r.Region, r.Currency, false));
                }
            }
        }

        // ── CoinGecko: crypto ─────────────────────────────────────────────────
        if (instrumentType is null or InstrumentType.CRYPTO)
        {
            var cgResults = await _coinGecko.SearchAsync(query);
            foreach (var r in cgResults.Where(r => !trackedSymbols.Contains(r.Symbol.ToUpper())))
            {
                results.Add(new InstrumentSearchResultDto(
                    r.Symbol.ToUpper(), r.Name, "CRYPTO", null, "USD", false));
            }
        }

        return Ok(results.Take(15).ToList());
    }

    private Guid? GetEntityId()
    {
        var claim = User.FindFirstValue("entityId");
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get all instruments, optionally filtered by type
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<InstrumentDto>>> GetInstruments([FromQuery] string? type = null)
    {
        InstrumentType? instrumentType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<InstrumentType>(type, true, out var parsedType))
            instrumentType = parsedType;

        var instruments = await _instrumentService.GetInstrumentsAsync(instrumentType);
        return Ok(instruments);
    }

    /// <summary>
    /// Get a specific instrument by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<InstrumentDto>> GetInstrumentById(Guid id)
    {
        var instrument = await _instrumentService.GetInstrumentByIdAsync(id);
        
        if (instrument == null)
            return NotFound(new { error = "Instrument not found." });

        return Ok(instrument);
    }

    /// <summary>
    /// Get an instrument by symbol
    /// </summary>
    [HttpGet("symbol/{symbol}")]
    public async Task<ActionResult<InstrumentDto>> GetInstrumentBySymbol(string symbol)
    {
        var instrument = await _instrumentService.GetInstrumentBySymbolAsync(symbol);
        
        if (instrument == null)
            return NotFound(new { error = $"Instrument with symbol '{symbol}' not found." });

        return Ok(instrument);
    }

    /// <summary>
    /// Create a new instrument
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<InstrumentDto>> CreateInstrument([FromBody] CreateInstrumentRequest request)
    {
        try
        {
            var instrument = await _instrumentService.CreateInstrumentAsync(request);
            return CreatedAtAction(nameof(GetInstrumentById), new { id = instrument.Id }, instrument);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing instrument
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<InstrumentDto>> UpdateInstrument(Guid id, [FromBody] UpdateInstrumentRequest request)
    {
        try
        {
            var instrument = await _instrumentService.UpdateInstrumentAsync(id, request);
            return Ok(instrument);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Immediately triggers a one-off price fetch for the given instrument, bypassing the
    /// 24-hour background job cycle. Useful for newly added instruments or manual refreshes.
    /// Returns the updated price and date. Returns 400 for MANUAL-priced instruments.
    /// </summary>
    [HttpPost("{id}/sync-price")]
    public async Task<IActionResult> SyncInstrumentPrice(Guid id)
    {
        var instrument = await _db.Instruments
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (instrument == null)
            return NotFound(new { error = "Instrument not found." });

        if (instrument.PriceProvider == null || instrument.PriceProvider == PriceProviderType.MANUAL)
            return BadRequest(new { error = "Manual instruments do not sync automatically. Enter prices via the price history section." });

        var entityId  = GetEntityId();
        var fetchSymbol = instrument.ExternalSymbol ?? instrument.Symbol;

        try
        {
            switch (instrument.PriceProvider.Value)
            {
                case PriceProviderType.YAHOO_FINANCE:
                    await _yahooFinance.FetchQuoteAsync(fetchSymbol);
                    break;

                case PriceProviderType.COINGECKO:
                    await _coinGecko.FetchPriceAsync(fetchSymbol, instrument.Symbol);
                    break;

                case PriceProviderType.ALPHA_VANTAGE:
                {
                    string? apiKey = entityId.HasValue
                        ? await _integrationService.GetEffectiveApiKeyAsync(entityId.Value, "ALPHA_VANTAGE")
                        : null;
                    apiKey ??= await _integrationService.GetEffectiveApiKeyAsync(Guid.Empty, "ALPHA_VANTAGE");
                    if (apiKey == null)
                        return BadRequest(new { error = "No Alpha Vantage API key configured. Add one in Settings → Integrations." });
                    await _alphaVantage.FetchQuoteAsync(fetchSymbol, apiKey);
                    break;
                }

                case PriceProviderType.METALS_API:
                {
                    string? apiKey = entityId.HasValue
                        ? await _integrationService.GetEffectiveApiKeyAsync(entityId.Value, "METALS_API")
                        : null;
                    apiKey ??= await _integrationService.GetEffectiveApiKeyAsync(Guid.Empty, "METALS_API");
                    if (apiKey == null)
                        return BadRequest(new { error = "No Metals API key configured. Add one in Settings → Integrations." });
                    await _metalsPriceService.FetchSpotPricesAsync(apiKey, "AUD");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = $"Price fetch failed: {ex.Message}" });
        }

        // Return the updated cached entry
        var cached = await _db.GlobalPriceCache
            .Where(g => g.Symbol == fetchSymbol || g.Symbol == instrument.Symbol)
            .OrderByDescending(g => g.AsOfDate)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            symbol    = instrument.Symbol,
            price     = cached?.Price,
            currency  = cached?.Currency,
            asOfDate  = cached?.AsOfDate.ToString("yyyy-MM-dd"),
            source    = cached?.Source.ToString()
        });
    }

    /// <summary>
    /// Delete an instrument (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInstrument(Guid id)
    {
        try
        {
            await _instrumentService.DeleteInstrumentAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
