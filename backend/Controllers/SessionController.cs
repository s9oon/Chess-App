using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("[controller]")]          // path /Session
public class SessionController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create()
    {
        // 1. already has a cookie? → return that id
        //    (User is filled in from the cookie by app.UseAuthentication() before we get here)
        string? existing = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (existing is not null)
            return Ok(new { id = existing });

        // 2. otherwise: new Guid → Claim → ClaimsIdentity → ClaimsPrincipal → HttpContext.SignInAsync(...)
        string id = Guid.NewGuid().ToString("N");

        var claim = new Claim(ClaimTypes.NameIdentifier, id);
        var identity = new ClaimsIdentity([claim], CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(principal);   // encrypts + signs the principal into the Set-Cookie header

        return Ok(new { id });
    }
}