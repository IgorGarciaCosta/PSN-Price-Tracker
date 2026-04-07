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
        var mensagem = "👋 *Bem-vindo ao PSN Price Tracker!*\n\n"
                     + "Eu sou o bot que monitora preços de jogos na PSN.\n\n"
                     + "📋 *Comandos disponíveis:*\n"
                     + "/buscar `(nome do jogo)` — Busca jogos na PSN\n"
                     + "/meusalertas — Lista seus alertas ativos\n"
                     + "/cancelar — Cancela um alerta ativo\n"
                     + "/gerarkey — Gera sua API Key\n\n"
                     + "🎮 Experimente: /buscar God of War";

        await _botApi.SendMessageAsync(botToken, chatId, mensagem, ct);
    }
}
