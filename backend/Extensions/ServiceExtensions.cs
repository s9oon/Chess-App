using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using backend.Data;

namespace backend.Extensions;

// Setup that used to sit in Program.cs, grouped by what it's for.
// Each method is an extension on IServiceCollection, so Program.cs can call builder.Services.AddX(...).
public static class ServiceExtensions
{
    public const string FrontendCors = "Frontend";

    // Postgres (EF Core) + Redis
    public static IServiceCollection AddDatabases(this IServiceCollection services, IConfiguration config)
    {
        string postgres = ConnectionStrings.Postgres(config.GetConnectionString("Postgres"));
        ConfigurationOptions redis = ConnectionStrings.Redis(config.GetConnectionString("Redis"));

        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(postgres));
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));

        return services;
    }

    // Lets the frontend origins from appsettings (Cors:AllowedOrigins) call the API with cookies
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration config)
    {
        string[] allowedOrigins = (config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();

        services.AddCors(options =>
            options.AddPolicy(FrontendCors, policy => policy
                .WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST")
                .AllowAnyHeader()
                .AllowCredentials()));

        return services;
    }

    // Anonymous session cookie
    public static IServiceCollection AddSessionCookies(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                // Frontend (vercel.app) and backend (trycloudflare.com) are different sites.
                // The default (Lax) means the browser keeps the cookie but won't send it on the
                // frontend's requests to the backend, so the hub sees no cookie -> 401.
                o.Cookie.SameSite = SameSiteMode.None;

                // Browsers reject SameSite=None cookies that aren't Secure. Always, because the app
                // only sees plain http from Traefik even though visitors use https.
                // (Browsers still accept Secure cookies on http://localhost, so local dev is fine.)
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

        return services;
    }
}
