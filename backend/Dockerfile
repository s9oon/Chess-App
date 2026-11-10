# ---------- build stage: full SDK, compiles and publishes ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on its own layer, so package downloads are cached until backend.csproj changes
COPY backend.csproj .
RUN dotnet restore

# Then the rest of the source (filtered by .dockerignore) and publish
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---------- runtime stage: just the ASP.NET runtime, much smaller ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Only the published output comes across: dlls, appsettings*.json, models/*.onnx
COPY --from=build /app .

# Same in every environment, so it's fine to bake in
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# ---------- runtime config: override with docker run -e / Azure app settings ----------
# Set these at runtime (Coolify env vars, docker run -e). Not baked into the image.
#   Cors__AllowedOrigins__0       frontend origin allowed to call the API (add __1, __2 ... for more)
#   ConnectionStrings__Postgres   Host=...;Port=5432;Database=...;Username=...;Password=...
#   ConnectionStrings__Redis      host:6379,password=...,abortConnect=false

# Run as the image's built-in non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "backend.dll"]
