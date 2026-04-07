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

public class InlineKeyboardMarkup
{
    [JsonPropertyName("inline_keyboard")]
    public List<List<InlineKeyboardButton>> InlineKeyboard { get; set; } = [];

    public InlineKeyboardMarkup() { }

    public InlineKeyboardMarkup(params InlineKeyboardButton[] singleRow)
    {
        InlineKeyboard = [new List<InlineKeyboardButton>(singleRow)];
    }
}

public class InlineKeyboardButton
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("callback_data")]
    public string? CallbackData { get; set; }
}
