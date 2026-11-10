using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using backend.Data;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController(ApplicationDbContext db, IConnectionMultiplexer redis) : ControllerBase
{
    [HttpGet("postgres")]
    public async Task<IActionResult> Postgres()
    {
        bool ok = await db.Database.CanConnectAsync();
        return ok ? Ok(new { postgres = true }) : StatusCode(503, new { postgres = false });
    }

    [HttpGet("redis")]
    public async Task<IActionResult> Redis()
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return Ok(new { redis = true });
        }
        catch (RedisException)
        {
            return StatusCode(503, new { redis = false });
        }
    }
}
