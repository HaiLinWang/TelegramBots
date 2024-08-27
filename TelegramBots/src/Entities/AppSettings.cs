using System.Collections.Generic;

namespace TelegramBots.Entities
{
    public class AppSettings
    {
        public string Name { get; set; }
        public bool Boolean { get; set; }
        public string TelegramBotToken { get; set; }
        public string DownloadPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
        public int DownloaderThreads { get; set; }
        public List<string> DemoList { get; set; } = new();
    }
}