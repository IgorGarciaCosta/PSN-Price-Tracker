using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
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

// 2. CONFIGURAÇÃO DO POLLY E SERVIÇOS
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

var app = builder.Build();

// 3. PIPELINE DE REQUISIÇÕES
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
