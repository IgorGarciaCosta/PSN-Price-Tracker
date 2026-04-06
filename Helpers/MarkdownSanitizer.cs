namespace PsnPriceTracker.Helpers;

/// <summary>
/// Escapes special characters for Telegram's legacy Markdown parse mode.
/// </summary>
public static class MarkdownSanitizer
{
    private static readonly char[] MarkdownChars = ['*', '_', '`', '['];

    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        foreach (var c in MarkdownChars)
            text = text.Replace(c.ToString(), $"\\{c}");

        return text;
    }
}
