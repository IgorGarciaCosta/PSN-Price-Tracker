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
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
    onRetry: (outcome, timespan, retryAttempt, context) =>
    {
        Console.WriteLine($"[POLLY] Falha no Telegram. Tentativa {retryAttempt}. Esperando {timespan.TotalSeconds}s...");
    });

builder.Services.AddHttpClient<IPsnIntegrationService, PsnIntegrationService>();
builder.Services.AddHttpClient<ITelegramIntegrationService, TelegramIntegrationService>()
    .AddPolicyHandler(retryPolicy);

builder.Services.AddScoped<IMonitoramentoService, MonitoramentoService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddHostedService<TelegramBotHostedService>();

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

// O Middleware roda antes dos controllers. Se barrar, nem chega no Controller.
app.UseApiKeyAuth();

app.MapControllers();

app.Run();
