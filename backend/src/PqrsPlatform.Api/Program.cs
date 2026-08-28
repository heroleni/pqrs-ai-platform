using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PqrsPlatform.Api.Cors;
using PqrsPlatform.Api.Hubs;
using PqrsPlatform.Api.Middleware;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Interfaces;
using PqrsPlatform.Infrastructure.AI;
using PqrsPlatform.Infrastructure.Auth;
using PqrsPlatform.Infrastructure.Persistence;
using PqrsPlatform.Infrastructure.Services;
using PqrsPlatform.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Los enums viajan como texto: el panel y el widget los muestran
        // tal cual y quedan legibles en cualquier cliente HTTP.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

builder.Services.AddScoped<ITenantContext, TenantContext>();

// --- Módulo de IA (RAG y Triaje) ------------------------------------------
builder.Services.AddAiServices(builder.Configuration, out var aiProvider);
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<TicketTriageService>();

// --- Autenticación / notificaciones ---------------------------------------
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ITicketNotifier, SignalRTicketNotifier>();

var connectionString =
    builder.Configuration["CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString, o => o.UseVector()));

builder.Services.AddCors();
builder.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Secret"];

if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JWT_ISSUER"]
                              ?? builder.Configuration["Jwt:Issuer"] ?? "pqrs-platform",
                ValidAudience = builder.Configuration["JWT_AUDIENCE"]
                                ?? builder.Configuration["Jwt:Audience"] ?? "pqrs-platform",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };

            opt.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var accessToken = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/tickets"))
                    {
                        ctx.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
}

builder.Services.AddAuthorization();

var app = builder.Build();

app.Logger.LogInformation("Proveedor de IA activo: {Provider}", aiProvider);


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors();

app.UseAuthentication();      
app.UseTenantResolution();    
app.UseAuthorization();       

app.MapControllers();
app.MapHub<TicketsHub>("/hubs/tickets");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxAttempts = 8;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await PqrsPlatform.Infrastructure.Persistence.DbSeeder.SeedAsync(db, embeddings, logger);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Intento {Attempt}/{Max} de conectar a la base de datos falló, reintentando en 5s...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

app.Run();