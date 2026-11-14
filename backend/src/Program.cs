using System.Text.Json.Serialization;
using backend.Extensions;
using backend.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddDatabases(builder.Configuration);
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddSessionCookies();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.UseHttpsRedirection();
app.UseCors(ServiceExtensions.FrontendCors);
app.UseAuthentication();   // reads the session cookie into User / Context.UserIdentifier
app.UseAuthorization();    // then [Authorize] checks it

app.MapControllers();
app.MapHub<ChessHub>("/hubs/chess");

app.Run();
