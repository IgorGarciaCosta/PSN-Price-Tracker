using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PsnPriceTracker.Data;
using PsnPriceTracker.Integrations;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Middleware;
using PsnPriceTracker.Services;
using PsnPriceTracker.Services.Callbacks;
using PsnPriceTracker.Services.Commands;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddControllers();

// 1. CONFIGURAÇÃO DO SWAGGER (Com o Cadeado visual)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PSN Price Tracker", Version = "v1" });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Insira a sua API Key no campo abaixo.",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            new string[] {}
        }
    });
});

// 2. BANCO DE DADOS (SQLite)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. SERVIÇOS

builder.Services.AddHttpClient<IPsnIntegrationService, PsnIntegrationService>();

builder.Services.AddSingleton<ITelegramBotApiService, TelegramBotApiService>();
builder.Services.AddSingleton<TelegramSessionManager>();
builder.Services.AddSingleton<TelegramCommandHandler>();
builder.Services.AddSingleton<TextoLivreHandler>();
builder.Services.AddSingleton<ITelegramCommand, StartCommandHandler>();
builder.Services.AddSingleton<ITelegramCommand, GerarKeyCommandHandler>();
builder.Services.AddSingleton<ITelegramCommand, BuscarCommandHandler>();
builder.Services.AddSingleton<ITelegramCommand, MeusAlertasCommandHandler>();
builder.Services.AddSingleton<ITelegramCommand, CancelarCommandHandler>();
builder.Services.AddSingleton<ITelegramCallbackHandler, BuscarCallbackHandler>();
builder.Services.AddSingleton<ITelegramCallbackHandler, CancelarCallbackHandler>();
builder.Services.AddScoped<IMonitoramentoService, MonitoramentoService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IAlertaService, AlertaService>();
builder.Services.AddHostedService<TelegramBotHostedService>();
builder.Services.AddHostedService<AlertaMonitorBackgroundService>();

// 4. RATE LIMITING (Proteção contra brute force)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(remoteIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// 5. AUTO-MIGRAÇÃO DO BANCO
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// 5. PIPELINE DE REQUISIÇÕES
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PSN Price Tracker v1");
    });
}

app.UseHttpsRedirection();

// Rate Limiter roda antes de tudo — bloqueia brute force antes de validar a API Key.
app.UseRateLimiter();

// O Middleware roda antes dos controllers. Se barrar, nem chega no Controller.
app.UseApiKeyAuth();

app.MapControllers();

app.Run();
