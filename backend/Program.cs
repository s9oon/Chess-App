using System.Text.Json.Serialization;
using Microsoft.ML.OnnxRuntime;
using backend.Chess;

var builder = WebApplication.CreateBuilder(args);

// ---------- services ----------

// Enums go over JSON as names ("Goataro") instead of numbers (0)
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// CORS: only the origins listed under "Cors:AllowedOrigins" in appsettings may call the API from a browser
const string FrontendCors = "Frontend";
// Blank entries are skipped, so an empty default (like the Dockerfile's ENV) means "no origins"
string[] allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST")
        .AllowAnyHeader()));

// One InferenceSession per model, loaded once and shared (Run is thread-safe).
// Paths come from the "Models" section, relative to the content root (the project folder, or the publish folder).
builder.Services.AddSingleton(_ =>
{
    var sessions = new Dictionary<ChessModel, InferenceSession>();

    foreach (ChessModel model in Enum.GetValues<ChessModel>())
    {
        string path = builder.Configuration[$"Models:{model}"]
            ?? throw new InvalidOperationException($"No path configured for model '{model}' (Models:{model}).");

        string fullPath = Path.Combine(builder.Environment.ContentRootPath, path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Model file for '{model}' not found.", fullPath);

        sessions[model] = new InferenceSession(fullPath);
    }

    return new Inference(sessions);
});

var app = builder.Build();

// Load the models now, so a missing file crashes on startup instead of on the first request
app.Services.GetRequiredService<Inference>();

// ---------- pipeline ----------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCors); // must come before the endpoints it applies to

app.UseAuthorization();

app.MapControllers();

app.Run();
