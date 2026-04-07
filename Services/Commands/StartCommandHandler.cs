using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services.Commands;

public class StartCommandHandler : ITelegramCommand
{
    private readonly ITelegramBotApiService _botApi;

    public string Command => "/start";

    public StartCommandHandler(ITelegramBotApiService botApi)
    {
        _botApi = botApi;
    }

    public async Task ExecuteAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        var mensagem = "👋 *Welcome to PSN Price Tracker!*\n\n"
                     + "I'm the bot that monitors PSN game prices.\n\n"
                     + "📋 *Available commands:*\n"
                     + "/search `(game name)` — Search games on PSN\n"
                     + "/myalerts — List your active alerts\n"
                     + "/cancel — Cancel an active alert\n"
                     + "/apikey — Generate your API Key\n\n"
                     + "🎮 Try it: /search God of War";

        await _botApi.SendMessageAsync(botToken, chatId, mensagem, ct);
    }
}
