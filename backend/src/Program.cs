using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

string postgres = ConnectionStrings.Postgres(builder.Configuration.GetConnectionString("Postgres"));
ConfigurationOptions redis = ConnectionStrings.Redis(builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(postgres));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));

const string FrontendCors = "Frontend";
string[] allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST")
        .AllowAnyHeader()
        .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.UseHttpsRedirection();

app.UseCors(FrontendCors);

app.UseAuthorization();

app.MapControllers();

app.Run();