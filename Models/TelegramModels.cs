using System.Text.Json.Serialization;

namespace PsnPriceTracker.Models;

public class TelegramResponse
{
    public bool Ok { get; set; }
    public List<TelegramUpdate>? Result { get; set; }
}

public class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }
    public TelegramMessage? Message { get; set; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; set; }
}

public class TelegramCallbackQuery
{
    public string? Id { get; set; }
    public string? Data { get; set; }
    public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    public string? Text { get; set; }
    public TelegramChat? Chat { get; set; }
}

public class TelegramChat
{
    public long? Id { get; set; }
}
