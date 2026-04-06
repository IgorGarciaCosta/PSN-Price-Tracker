using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using PsnPriceTracker.Data;
using PsnPriceTracker.Integrations;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Middleware;
using PsnPriceTracker.Services;

var builder = WebApplication.CreateBuilder(args);

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

// 3. CONFIGURAÇÃO DO POLLY E SERVIÇOS
var retryLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Polly");

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
    onRetry: (outcome, timespan, retryAttempt, context) =>
    {
        retryLogger.LogWarning("[POLLY] Falha no Telegram. Tentativa {RetryAttempt}. Esperando {Delay}s...", retryAttempt, timespan.TotalSeconds);
    });

builder.Services.AddHttpClient<IPsnIntegrationService, PsnIntegrationService>();
builder.Services.AddHttpClient<ITelegramIntegrationService, TelegramIntegrationService>()
    .AddPolicyHandler(retryPolicy);

builder.Services.AddSingleton<ITelegramBotApiService, TelegramBotApiService>();
builder.Services.AddSingleton<TelegramCommandHandler>();
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

// 4. AUTO-CRIAÇÃO DO BANCO
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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
