# ── Etapa 1: Build ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar el archivo de proyecto y restaurar dependencias
COPY Inventario.WebAPI/Inventario.WebAPI.csproj Inventario.WebAPI/
RUN dotnet restore Inventario.WebAPI/Inventario.WebAPI.csproj

# Copiar todo el código fuente del API
COPY Inventario.WebAPI/ Inventario.WebAPI/

# Publicar en modo Release
RUN dotnet publish Inventario.WebAPI/Inventario.WebAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Etapa 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copiar los archivos publicados
COPY --from=build /app/publish .

# Crear wwwroot y copiar el frontend
RUN mkdir -p wwwroot
COPY index.html wwwroot/
COPY app.js wwwroot/
COPY stockpoint_logo.png wwwroot/

EXPOSE 8080

# Program.cs lee la variable PORT en tiempo de ejecucion
ENTRYPOINT ["dotnet", "Inventario.WebAPI.dll"]
