# PSN Price Tracker

A web API built with ASP.NET Core (.NET 9) to monitor PlayStation Store game prices and send Telegram alerts when a game reaches your target price.

## Features

- Fetches the current price of a PSN game from a given URL
- Compares the current price against a user-defined target price
- Sends a Telegram notification when the price meets or drops below the target
- Interactive API documentation via Swagger UI

> **Note:** PSN price lookup and Telegram messaging currently use mock data. Real integrations are planned for future releases.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/IgorGarciaCosta/PSN-Price-Tracker.git
   cd PSN-Price-Tracker
   ```

2. Run the application:

   ```bash
   dotnet run
   ```

3. The API will be available at:
   - HTTP: `http://localhost:5023`
   - HTTPS: `https://localhost:7200`

4. Access the interactive documentation (Swagger UI) in Development mode:
   - `http://localhost:5023/swagger`

## Main Endpoint

### `POST /api/alertas/testar`

Tests a price alert for a PSN game.

**Request Body:**

```json
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

## Project Structure

```
├── Controllers/
│   └── AlertasController.cs         # Price alert controller
├── Integrations/
│   ├── PsnIntegrationService.cs     # PSN integration (mock)
│   └── TelegramIntegrationService.cs # Telegram integration (mock)
├── Interfaces/
│   ├── IMonitoramentoService.cs     # Monitoring service contract
│   ├── IPsnIntegrationService.cs    # PSN integration contract
│   └── ITelegramIntegrationService.cs # Telegram integration contract
├── Models/
│   ├── AlertaRequestDTO.cs          # Request DTO (game URL + target price)
│   └── PrecoPsnDTO.cs               # Game data DTO (name + current price)
├── Services/
│   └── MonitoramentoService.cs      # Price monitoring and comparison logic
├── Properties/
│   └── launchSettings.json          # Launch profiles
├── Program.cs                       # Entry point and DI/pipeline configuration
├── PsnPriceTracker.csproj           # Project dependencies and build settings
└── appsettings.json                 # Application configuration
```

## Architecture

The project follows a layered architecture with dependency injection:

1. **Controller** — Receives the HTTP request and delegates to the service layer
2. **Service** — Orchestrates business logic (fetch price → compare → notify)
3. **Integrations** — Communication with external services (PSN and Telegram)
4. **Interfaces** — Contracts that decouple the layers

## Tech Stack

- **Framework:** ASP.NET Core 9
- **Language:** C#
- **API Docs:** Swagger UI (Swashbuckle) + Microsoft.AspNetCore.OpenApi
- **Patterns:** Dependency Injection, DTO, Interface Segregation
