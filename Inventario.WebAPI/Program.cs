using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using Inventario.WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Puerto dinámico para Railway ─────────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── CORS: Permitir todo (frontend y API en el mismo dominio) ─────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── Servicios ────────────────────────────────────────────────────
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddScoped<AuthService>();

// ── JWT Authentication ────────────────────────────────────────────
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "EnterpriseCRMSecretKey2026SuperSecureAndLongEnoughToAvoidSignatureErrors";
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = "InventarioAppServer",
        ValidateAudience = true,
        ValidAudience = "InventarioAppClient",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Archivos estáticos (index.html, app.js, logo) ────────────────
app.UseDefaultFiles();                  // Sirve index.html por defecto
app.UseStaticFiles();                   // Sirve archivos estáticos

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Fallback: todas las rutas no-API devuelven index.html (SPA) ──
app.MapFallbackToFile("index.html");

app.Run();
