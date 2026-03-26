using PsnPriceTracker.Integrations;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IPsnIntegrationService, PsnIntegrationService>();
builder.Services.AddScoped<ITelegramIntegrationService, TelegramIntegrationService>();
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
