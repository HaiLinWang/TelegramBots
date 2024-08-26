using TelegramBots.Utilities;

namespace TelegramBots.Entities;

public class Sticker
{
    public string Name { get; set; } = "None";
    public string Link { get; set; } = "None";
    public string Emoji { get; set; } = "😀";
    public string FileType { get; set; } = "webp";

    public Sticker() { }

    public Sticker(string name, string link, string emoji, string fileType)
    {
        Name = name;
        Link = link;
        Emoji = emoji;
        FileType = fileType;
    }

    public override string ToString()
    {
        return $"<Sticker:{Name}>";
    }

    public string EmojiName()
    {
        return EmojiHelper.Demojize(Emoji);
    }
}