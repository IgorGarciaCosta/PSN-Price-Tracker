# PSN Price Tracker

A web application built with ASP.NET Core (.NET 9) that monitors PlayStation Store game prices and sends Telegram alerts when a game reaches your target price. Includes a fully interactive Telegram bot and a REST API.

## Features

- Search PSN Store games by name directly from Telegram
- Set price alerts with a target price per game
- Automatic background monitoring every 60 minutes (configurable)
- Telegram notifications when a game's price drops to or below your target
- Web scraping of PSN Store product pages for real-time pricing
- REST API with API Key authentication and rate limiting
- Interactive API documentation via Swagger UI
- SQLite database for persistent storage
- Docker support for easy deployment

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Telegram Bot Token (from [@BotFather](https://t.me/BotFather))
- (Optional) [Docker](https://www.docker.com/) for containerized deployment

## Getting Started

### Local Development

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

5. Swagger UI is enabled automatically in Development mode:
   - `http://localhost:5023/swagger`

### Docker Deployment

1. Create a `.env` file in the project root:

   ```env
   TELEGRAM_BOT_TOKEN=your-bot-token-here
   MONITORAMENTO_INTERVALO=60
   ```

2. Build and run:

   ```bash
   docker-compose up -d --build
   ```

3. The API will be available at `http://localhost:8080`

4. To enable Swagger UI in production, the `EnableSwagger=true` environment variable is already set in `docker-compose.yaml`. Access it at:
   - `http://<your-server-ip>:8080/swagger`

### Oracle Cloud Deployment

When deploying to Oracle Cloud, make sure to:

1. **Security List** — Add an Ingress Rule for TCP port `8080` with source `0.0.0.0/0`
2. **VM Firewall** — Open the port in iptables:
   ```bash
   sudo iptables -I INPUT -p tcp --dport 8080 -j ACCEPT
   sudo netfilter-persistent save
   ```

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

| Method | Route                               | Description                   |
| ------ | ----------------------------------- | ----------------------------- |
| `GET`  | `/api/alertas/buscar-jogo?nome=...` | Search PSN Store by game name |

### Example — Search Game

**Request:**

```http
GET /api/alertas/buscar-jogo?nome=God of War
X-Api-Key: your-api-key-here
```

**Response (200 OK):**

```json
[
  {
    "nomeDoJogo": "God of War Ragnarök",
    "urlDoJogo": "https://store.playstation.com/pt-br/product/...",
    "plataforma": "PS5",
    "imagemUrl": "https://...",
    "precoAtual": "R$ 149,90"
  }
]
```

**Response (404 Not Found):**

```json
{
  "mensagem": "Nenhum jogo encontrado para 'xyz'."
}
```

## Configuration

| Key                                   | Default                     | Description                                   |
| ------------------------------------- | --------------------------- | --------------------------------------------- |
| `Telegram:BotToken`                   | _(empty)_                   | Bot token from @BotFather **(required)**      |
| `Monitoramento:IntervaloMinutos`      | `60`                        | Background monitor interval in minutes        |
| `ConnectionStrings:DefaultConnection` | `Data Source=psntracker.db` | SQLite database file path                     |
| `EnableSwagger`                       | `false`                     | Enable Swagger UI outside of Development mode |

## Project Structure

```
├── Controllers/
│   ├── AlertasController.cs              # REST API endpoints
│   └── Middleware/
│       ├── ApiKeyMiddleware.cs           # API Key validation middleware
│       └── ApiKeyMiddlewareExtensions.cs # Middleware registration extension
├── Data/
│   └── AppDbContext.cs                   # EF Core DbContext (SQLite)
├── Helpers/
│   └── MarkdownSanitizer.cs             # Telegram Markdown escaping
├── Integrations/
│   └── PsnIntegrationService.cs         # PSN Store web scraping & search
├── Interfaces/
│   ├── IAlertaService.cs                # Alert CRUD contract
│   ├── IApiKeyService.cs               # API Key service contract
│   ├── IMonitoramentoService.cs         # Monitoring service contract
│   ├── IPsnIntegrationService.cs        # PSN integration contract
│   ├── ITelegramBotApiService.cs        # Telegram Bot API contract
│   ├── ITelegramCallbackHandler.cs      # Callback button handler contract
│   └── ITelegramCommand.cs             # Bot command handler contract
├── Models/
│   ├── AlertaEntity.cs                  # Alert database entity
│   ├── AlertaRequestDTO.cs             # Alert request DTO
│   ├── ApiKeyEntity.cs                 # API Key database entity
│   ├── BuscaResultadoDTO.cs            # Game search result DTO
│   ├── PrecoPsnDTO.cs                  # PSN price data DTO
│   └── TelegramModels.cs              # Telegram API response models
├── Services/
│   ├── Callbacks/
│   │   ├── BuscarCallbackHandler.cs    # Game selection callback
│   │   └── CancelarCallbackHandler.cs  # Alert cancel callback
│   ├── Commands/
│   │   ├── BuscarCommandHandler.cs     # /buscar command
│   │   ├── CancelarCommandHandler.cs   # /cancelar command
│   │   ├── GerarKeyCommandHandler.cs   # /gerarkey command
│   │   ├── MeusAlertasCommandHandler.cs# /meusalertas command
│   │   ├── StartCommandHandler.cs      # /start command
│   │   └── TextoLivreHandler.cs        # Free text (price input)
│   ├── AlertaMonitorBackgroundService.cs # Scheduled price monitoring
│   ├── AlertaService.cs                # Alert CRUD operations
│   ├── ApiKeyService.cs               # API Key generation & validation
│   ├── MonitoramentoService.cs         # Price comparison logic
│   ├── TelegramBotApiService.cs        # Telegram Bot API HTTP client
│   ├── TelegramBotHostedService.cs     # Long polling hosted service
│   ├── TelegramCommandHandler.cs       # Command dispatcher
│   └── TelegramSessionManager.cs      # User session state management
├── Program.cs                          # Entry point, DI & pipeline config
├── Dockerfile                          # Multi-stage Docker build
├── docker-compose.yaml                 # Docker Compose configuration
├── appsettings.json                    # Application configuration
└── PsnPriceTracker.csproj              # Project file & dependencies
```

## Architecture

The project follows a layered architecture with dependency injection and the Command Pattern for Telegram bot handling:

1. **Controllers** — HTTP entry points, protected by API Key middleware and rate limiting
2. **Services/Commands** — Each Telegram command (`/buscar`, `/cancelar`, etc.) is an `ITelegramCommand` implementation, dispatched by `TelegramCommandHandler`
3. **Services/Callbacks** — Inline button handlers implementing `ITelegramCallbackHandler`
4. **Services** — Business logic (alert CRUD, price monitoring, session management)
5. **Integrations** — External communication (PSN web scraping)
6. **Data** — EF Core with SQLite, auto-migration on startup
7. **Background Services** — `AlertaMonitorBackgroundService` (scheduled price checks) and `TelegramBotHostedService` (long polling)

## Tech Stack

- **Framework:** ASP.NET Core 9
- **Language:** C#
- **Database:** SQLite with Entity Framework Core
- **HTML Parsing:** HtmlAgilityPack
- **Resilience:** Polly (retry policies with exponential backoff)
- **API Docs:** Swagger UI (Swashbuckle) + Scalar
- **Containerization:** Docker + Docker Compose
- **Patterns:** Command Pattern, Dependency Injection, DTO, Interface Segregation, Background Services
