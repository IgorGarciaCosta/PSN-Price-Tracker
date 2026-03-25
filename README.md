# PSN Price Tracker

A web API built with ASP.NET Core (.NET 9) for tracking PlayStation Store game prices.

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

## API Documentation

OpenAPI documentation is available in development mode at `/openapi/v1.json`.

## Project Structure

| File / Folder                    | Description                                        |
| -------------------------------- | -------------------------------------------------- |
| `Program.cs`                     | Application entry point and endpoint configuration |
| `PsnPriceTracker.csproj`         | Project file with dependencies and build settings  |
| `Properties/launchSettings.json` | Development launch profiles                        |
| `appsettings.json`               | Application configuration                          |

## Tech Stack

- **Framework:** ASP.NET Core 9 (Minimal API)
- **Language:** C# 13
- **API Docs:** Microsoft.AspNetCore.OpenApi
