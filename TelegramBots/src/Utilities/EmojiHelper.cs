using System.Globalization;
using System.Text.RegularExpressions;

namespace TelegramBots.Utilities;

public static class EmojiHelper
{
    public static string Demojize(string emoji)
    {
        if (string.IsNullOrEmpty(emoji))
        {
            return "unknown";
        }
        var parts = new HashSet<string>();
        for (var i = 0; i < emoji.Length; i++)
        {
            if (char.IsSurrogatePair(emoji, i))
            {
                var codePoint = char.ConvertToUtf32(emoji, i);
                var name = GetEmojiDescription(codePoint);
                if (!string.IsNullOrEmpty(name))
                {
                    parts.UnionWith(SplitAndCleanName(name));
                }
                i++; // Skip the low surrogate
            }
            else
            {
                var name = GetEmojiDescription(emoji[i]);
                if (!string.IsNullOrEmpty(name))
                {
                    parts.UnionWith(SplitAndCleanName(name));
                }
            }
        }

        return string.Join("_", parts.OrderBy(p => p));
    }
    private static string GetEmojiDescription(int codePoint)
    {
        var category = char.GetUnicodeCategory(char.ConvertFromUtf32(codePoint)[0]);
        if (category == UnicodeCategory.OtherSymbol || category == UnicodeCategory.OtherNotAssigned)
        {
            return $"U+{codePoint:X4}";
        }
        return null;
    }
    private static string GetEmojiDescription(char c)
    {
        if (char.GetUnicodeCategory(c) == UnicodeCategory.OtherSymbol)
        {
            return $"U+{(int)c:X4}";
        }
        return null;
    }
    private static IEnumerable<string> SplitAndCleanName(string name)
    {
        return name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Regex.Replace(part, @"[^a-zA-Z0-9]", "").ToLower())
            .Where(part => part != "with" && !string.IsNullOrEmpty(part));
    }
}