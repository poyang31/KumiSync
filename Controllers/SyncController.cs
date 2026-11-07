using System.Text.Json;
using KimiSync.Services;
using Microsoft.AspNetCore.Mvc;

namespace KimiSync.Controllers;

[ApiController]
[Route("sync/data")]
public sealed class SyncController(KumiTokenService tokens, SyncDataStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var id = tokens.Validate(Request.Headers["X-Kumi-Token"].ToString());
        if (id is null) return Unauthorized();
        var data = await store.Get(id, cancellationToken);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPut]
    public async Task<IActionResult> Put(CancellationToken cancellationToken)
    {
        var id = tokens.Validate(Request.Headers["X-Kumi-Token"].ToString());
        if (id is null) return Unauthorized();
        if (!Request.HasJsonContentType()) return BadRequest();
        try
        {
            using var body = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            if (body.RootElement.ValueKind != JsonValueKind.Object ||
                !body.RootElement.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(content.GetString()))
                return BadRequest();
            return Ok(await store.Put(id, content.GetString()!, cancellationToken));
        }
        catch (JsonException) { return BadRequest(); }
    }
}
