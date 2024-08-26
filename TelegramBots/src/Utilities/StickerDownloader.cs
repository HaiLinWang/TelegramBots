using TelegramBots.Entities;

namespace TelegramBots.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class StickerDownloader
{
    private readonly int threads;
    private readonly string token;
    private readonly string cwd;
    private readonly HttpClient httpClient;
    private readonly string api;

    public StickerDownloader(string token, int multithreading = 4)
    {
        this.threads = multithreading;
        this.token = token;
        this.cwd = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
        Directory.CreateDirectory(this.cwd);
        this.httpClient = new HttpClient();
        this.api = $"https://api.telegram.org/bot{this.token}/";
        VerifyToken();
    }

    private void VerifyToken()
    {
        var verify = DoAPIReq("getMe", new Dictionary<string, string>()).Result;
        if (verify != null && verify["ok"].Value<bool>())
        {
            return;
        }

        Console.WriteLine("Invalid token.");
        Environment.Exit(1);
    }

    private async Task<JObject> DoAPIReq(string function, Dictionary<string, string> parameters)
    {
        var urlParams = string.Join("&", parameters.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
        var url = $"{api}{function}?{urlParams}";
        var response = await httpClient.GetStringAsync(url);
        return JObject.Parse(response);
    }

    public async Task<Sticker> GetSticker(JObject fileData)
    {
        var info = await DoAPIReq("getFile",
            new Dictionary<string, string> { { "file_id", fileData["file_id"].ToString() } });
        if (info != null)
        {
            var filePath = info["result"]["file_path"].ToString();
            return new Sticker(
                name: Path.GetFileName(filePath),
                link: $"https://api.telegram.org/file/bot{token}/{filePath}",
                emoji: fileData["emoji"].ToString(),
                fileType: Path.GetExtension(filePath).TrimStart('.')
            );
        }

        return new Sticker();
    }

    public async Task<Dictionary<string, object>> GetPack(string packName)
    {
        var res = await DoAPIReq("getStickerSet", new Dictionary<string, string> { { "name", packName } });
        if (res == null)
        {
            return null;
        }
        var stickers = res["result"]["stickers"].ToObject<JArray>();
        var files = new List<Sticker>();
     
        Console.WriteLine($"Starting to scrape \"{packName}\" ..");
        var start = DateTime.Now;
        var tasks = stickers.Select(s => GetSticker(s.ToObject<JObject>()));
        files = (await Task.WhenAll(tasks)).ToList();
        var end = DateTime.Now;
        // 获取唯一的文件后缀名
        var folders = files.Select(f => f.FileType).Distinct().ToList();
        Console.WriteLine($"Time taken to scrape {files.Count} stickers - {(end - start).TotalSeconds:F3}s");
        Console.WriteLine();
        return new Dictionary<string, object>
        {
            { "name", res["result"]["name"].ToString().ToLower() },
            { "title", res["result"]["title"].ToString() },
            { "files", files },
            {"folders",folders},
            { "total_count", files.Count }
        };
    }

    public async Task<int> DownloadSticker(string path, string link)
    {
        var response = await httpClient.GetByteArrayAsync(link);
        await File.WriteAllBytesAsync(path, response);
        return response.Length;
    }

    public async Task<bool> DownloadPack(Dictionary<string, object> pack, bool forceRedownload = false)
    {
        var swd = Path.Combine(cwd, pack["name"].ToString());
        if (forceRedownload && Directory.Exists(swd))
        {
            // 如果强制重新下载，先清空文件夹
            Directory.Delete(swd, true);
        }
        Directory.CreateDirectory(swd);
        var downloads = 0;
        Console.WriteLine($"Starting to download \"{pack["name"]}\" to {swd}");
        var start = DateTime.Now;
        var files = (List<Sticker>)pack["files"];
        var tasks = files.Select(sticker =>
        {
            var filePath = Path.Combine(swd, sticker.FileType,
                $"{Path.GetFileNameWithoutExtension(sticker.Name).Split('_').Last()}+{sticker.EmojiName()}.{sticker.FileType}");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            return DownloadSticker(filePath, sticker.Link);
        });

        var results = await Task.WhenAll(tasks);
        downloads = results.Count(r => r > 0);

        var end = DateTime.Now;
        Console.WriteLine($"Downloaded {downloads} stickers in {(end - start).TotalSeconds:F3}s");
        Console.WriteLine();

        return downloads == files.Count;
    }
}