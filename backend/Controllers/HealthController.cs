using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using backend.Data;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController(ApplicationDbContext db, IConnectionMultiplexer redis) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        bool postgres = await db.Database.CanConnectAsync();

        bool redisOk;
        try
        {
            await redis.GetDatabase().PingAsync();
            redisOk = true;
        }
        catch (RedisException)
        {
            redisOk = false;
        }

        var result = new { postgres, redis = redisOk };
        return postgres && redisOk ? Ok(result) : StatusCode(503, result);
    }
}
