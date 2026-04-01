# PSN Price Tracker

A web application built with ASP.NET Core (.NET 9) that monitors PlayStation Store game prices and sends Telegram alerts when a game reaches your target price. Includes a fully interactive Telegram bot and a REST API.

## Features

- Search PSN Store games by name directly from Telegram
- Set price alerts with a target price per game
- Automatic background monitoring every 60 minutes (configurable)
- Telegram notifications when a game's price drops to or below your target
- Web scraping of PSN Store product pages for real-time pricing
- REST API with API Key authentication
- Interactive API documentation via Swagger UI
- SQLite database for persistent storage

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Telegram Bot Token (from [@BotFather](https://t.me/BotFather))

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/IgorGarciaCosta/PSN-Price-Tracker.git
   cd PSN-Price-Tracker
   ```

2. Configure your Telegram bot token in `appsettings.json`:

   ```json
   {
     "Telegram": {
       "BotToken": "YOUR_BOT_TOKEN_HERE"
     }
   }
   ```

3. Run the application:

   ```bash
   dotnet run
   ```

4. The API will be available at:
   - HTTP: `http://localhost:5023`
   - HTTPS: `https://localhost:7200`

5. Access the interactive documentation (Swagger UI) in Development mode:
   - `http://localhost:5023/swagger`

## Telegram Bot Commands

| Command          | Description                                                              |
| ---------------- | ------------------------------------------------------------------------ |
| `/start`         | Welcome message with available commands                                  |
| `/buscar <nome>` | Search PSN Store for games (returns up to 5 results with inline buttons) |
| `/meusalertas`   | List all your active price alerts                                        |
| `/cancelar`      | Deactivate an existing alert (shows cancel buttons)                      |
| `/gerarkey`      | Generate an API Key for REST API access                                  |

### Alert Flow

1. Send `/buscar God of War` to the bot
2. Bot returns game cards with images and "Escolher este ✅" buttons
3. Select a game — bot asks for your target price
4. Reply with the price (e.g. `150`, `150.00`, or `R$ 150,00`)
5. Alert is saved and the current price is checked immediately
6. Background monitor checks all active alerts every 60 minutes

## REST API Endpoints

All endpoints require the `X-Api-Key` header (except Swagger docs). Generate a key via the `/gerarkey` Telegram command.

| Method | Route                               | Description                          |
| ------ | ----------------------------------- | ------------------------------------ |
| `GET`  | `/api/alertas/buscar-jogo?nome=...` | Search PSN Store by game name        |
| `POST` | `/api/alertas/testar`               | Test a price alert immediately       |
| `GET`  | `/api/alertas/jogos-mock`           | Returns mock game data (for testing) |

### Example — Test Alert

**Request:**

```http
POST /api/alertas/testar
X-Api-Key: your-api-key-here
Content-Type: application/json

{
  "urlDoJogo": "https://store.playstation.com/pt-br/product/...",
  "precoAlvo": 100.0
}
```

**Response (price reached):**

```json
{
  "mensagem": "Preço atingido! Notificação enviada."
}
```

**Response (price above target):**

```json
{
  "mensagem": "O preço atual (R$ 149,90) ainda está acima do seu alvo."
}
```

## Configuration

| Key                                   | Default                     | Description                              |
| ------------------------------------- | --------------------------- | ---------------------------------------- |
| `Telegram:BotToken`                   | _(empty)_                   | Bot token from @BotFather **(required)** |
| `Monitoramento:IntervaloMinutos`      | `60`                        | Background monitor interval in minutes   |
| `ConnectionStrings:DefaultConnection` | `Data Source=psntracker.db` | SQLite database file path                |

## Project Structure

```
├── Controllers/
│   ├── AlertasController.cs              # REST API endpoints
│   └── Middleware/
│       ├── ApiKeyMiddleware.cs           # API Key validation middleware
│       └── ApiKeyMiddlewareExtensions.cs # Middleware registration extension
├── Data/
│   └── AppDbContext.cs                   # EF Core DbContext (SQLite)
├── Integrations/
│   ├── PsnIntegrationService.cs          # PSN Store web scraping & search
│   └── TelegramIntegrationService.cs     # Telegram message sending
├── Interfaces/
│   ├── IAlertaService.cs                 # Alert CRUD contract
│   ├── IApiKeyService.cs                 # API Key service contract
│   ├── IMonitoramentoService.cs          # Monitoring service contract
│   ├── IPsnIntegrationService.cs         # PSN integration contract
│   ├── ITelegramBotApiService.cs         # Telegram Bot API contract
│   └── ITelegramIntegrationService.cs    # Telegram integration contract
├── Models/
│   ├── AlertaEntity.cs                   # Alert database entity
│   ├── AlertaRequestDTO.cs              # Alert request DTO
│   ├── ApiKeyEntity.cs                  # API Key database entity
│   ├── BuscaResultadoDTO.cs             # Game search result DTO
│   ├── PrecoPsnDTO.cs                   # PSN price data DTO
│   └── TelegramModels.cs               # Telegram API response models
├── Services/
│   ├── AlertaMonitorBackgroundService.cs # Scheduled price monitoring job
│   ├── AlertaService.cs                 # Alert CRUD operations
│   ├── ApiKeyService.cs                 # API Key generation & validation
│   ├── MonitoramentoService.cs          # Price comparison logic
│   ├── TelegramBotApiService.cs         # Telegram Bot API HTTP client
│   ├── TelegramBotHostedService.cs      # Long polling hosted service
│   └── TelegramCommandHandler.cs        # Bot command routing & state
├── Program.cs                           # Entry point, DI & pipeline config
├── appsettings.json                     # Application configuration
└── PsnPriceTracker.csproj               # Project file & dependencies
```

## Architecture

The project follows a layered architecture with dependency injection:

1. **Controllers** — HTTP entry points, protected by API Key middleware
2. **Services** — Business logic (alert CRUD, price monitoring, command handling)
3. **Integrations** — External communication (PSN web scraping, Telegram API)
4. **Data** — EF Core with SQLite, auto-migration on startup
5. **Background Services** — `AlertaMonitorBackgroundService` (scheduled price checks) and `TelegramBotHostedService` (long polling)

## Tech Stack

- **Framework:** ASP.NET Core 9
- **Language:** C#
- **Database:** SQLite with Entity Framework Core
- **HTML Parsing:** HtmlAgilityPack
- **Resilience:** Polly (retry policies with exponential backoff)
- **API Docs:** Swagger UI (Swashbuckle) + Scalar
- **Patterns:** Dependency Injection, DTO, Interface Segregation, Background Services
