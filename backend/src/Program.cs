using System.Text.Json.Serialization;
using backend.Chess.Engine.Inference;
using backend.Chess.Engine.MCTS;
using backend.Extensions;
using backend.Hubs;
using backend.Interface;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IChessService, ChessService>();   // asking for IChessService gives you this one ChessService
builder.Services.Configure<MctsSettings>(builder.Configuration.GetSection("Mcts"));
builder.Services.AddSingleton<IChessEngine, ChessEngine>();     // loads the .onnx models once, shared by every match

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
app.MapHub<CounterHub>("/hubs/counter");
app.MapHub<ChessHub>("/hubs/chess");

app.Run();
