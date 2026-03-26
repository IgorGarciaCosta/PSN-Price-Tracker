using PsnPriceTracker.Integrations;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Services;
using Polly;
using Polly.Extensions.Http;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();


// --- INÍCIO DA CONFIGURAÇÃO DO POLLY ---
// Define a política: Se der erro de rede ou erro 5xx, tenta 3 vezes.
// O tempo de espera aumenta exponencialmente: 2s, 4s, 8s...
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
    onRetry: (outcome, timespan, retryAttempt, context) =>
    {
        // Esse log é ótimo para o recrutador ver que o Polly realmente está funcionando
        Console.WriteLine($"[POLLY AVISO] Falha ao contatar Telegram. Tentativa {retryAttempt} de 3. Esperando {timespan.TotalSeconds}s...");
    });

// Registra o serviço do Telegram com o HttpClient e atrela a política do Polly
builder.Services.AddHttpClient<ITelegramIntegrationService, TelegramIntegrationService>()
    .AddPolicyHandler(retryPolicy);
// --- FIM DA CONFIGURAÇÃO DO POLLY ---

builder.Services.AddHttpClient<IPsnIntegrationService, PsnIntegrationService>();
builder.Services.AddScoped<IMonitoramentoService, MonitoramentoService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "PSN Price Tracker");
    });
}

app.UseHttpsRedirection();
app.MapControllers();


app.Run();
