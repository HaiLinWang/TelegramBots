using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TelegramBots.Entities;
using TelegramBots.Utilities;

namespace TelegramBots.Services;

public interface IStickerDownloaderService
{
    Task InitializeAsync();
    // Task<Dictionary<string, object>> GetPack(string packName);
    // Task<bool> DownloadPack(Dictionary<string, object> pack, bool forceRedownload = false);
}

public class StickerDownloaderService : IStickerDownloaderService
{
    private readonly ILogger<StickerDownloaderService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _cwd;
    private readonly string _api;
    private readonly int _threads;

    public StickerDownloaderService(
        ILogger<StickerDownloaderService> logger, IOptions<AppSettings> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("StickerDownloader");
        _token = options.Value.TelegramBotToken;
        _threads = options.Value.DownloaderThreads;
        _cwd = Path.Combine(Directory.GetCurrentDirectory(), "downloads");
        _api = $"https://api.telegram.org/bot{_token}/";
    }
    
    public async Task InitializeAsync()
    {
        // await VerifyTokenAsync();
        await VerifyFileAsync();
    }
    // private async Task VerifyTokenAsync()
    // {
    //     try
    //     {
    //         var verify = await DoAPIReq("getMe", new Dictionary<string, string>());
    //         if (verify != null && verify["ok"].Value<bool>())
    //         {
    //             _logger.LogInformation("令牌验证成功。");
    //             return;
    //         }
    //
    //         _logger.LogError("无效的令牌。");
    //         throw new InvalidOperationException("无效的令牌");
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "令牌验证过程中发生错误。");
    //         throw;
    //     }
    // }
    private Task VerifyFileAsync()
    {
        try
        {
            if (!Directory.Exists(_cwd))
            {
                Directory.CreateDirectory(_cwd);
                _logger.LogInformation($"成功创建下载文件夹：{_cwd}");
            }
            else
            {
                _logger.LogInformation($"下载文件夹已存在：{_cwd}");
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"创建或验证下载文件夹 {_cwd} 时发生错误。");
            throw new InvalidOperationException($"无法创建或访问下载文件夹：{_cwd}", ex);
        }
    }
    //
    // private async Task<JObject> DoAPIReq(string function, Dictionary<string, string> parameters)
    // {
    //     var urlParams = string.Join("&", parameters.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
    //     var url = $"{_api}{function}?{urlParams}";
    //     try
    //     {
    //         _logger.LogInformation($"URL: {url}...");
    //         var response = await _httpClient.GetStringAsync(url);
    //         _logger.LogInformation($"Received response: {response.Substring(0, Math.Min(response.Length, 100))}...");
    //         return JObject.Parse(response);
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogInformation($"API request failed for URL: {url}+error:{e.Message}");
    //         throw;
    //     }
    // }
    //
    // private async Task<Sticker> GetSticker(JObject fileData)
    // {
    //     var info = await DoAPIReq("getFile",
    //         new Dictionary<string, string> { { "file_id", fileData["file_id"].ToString() } });
    //     if (info != null)
    //     {
    //         var filePath = info["result"]["file_path"].ToString();
    //         return new Sticker(
    //             name: Path.GetFileName(filePath),
    //             link: $"https://api.telegram.org/file/bot{_token}/{filePath}",
    //             emoji: fileData["emoji"].ToString(),
    //             fileType: Path.GetExtension(filePath).TrimStart('.')
    //         );
    //     }
    //
    //     return new Sticker();
    // }
    //
    // public async Task<Dictionary<string, object>> GetPack(string packName)
    // {
    //     var res = await DoAPIReq("getStickerSet", new Dictionary<string, string> { { "name", packName } });
    //     if (res == null)
    //     {
    //         return null;
    //     }
    //
    //     var stickers = res["result"]["stickers"].ToObject<JArray>();
    //     var files = new List<Sticker>();
    //
    //     _logger.LogInformation($"开始抓取贴纸包 {packName}...");
    //     var start = DateTime.Now;
    //     var tasks = stickers.Select(s => GetSticker(s.ToObject<JObject>()));
    //     files = (await Task.WhenAll(tasks)).ToList();
    //     var end = DateTime.Now;
    //     var folders = files.Select(f => f.FileType).Distinct().ToList();
    //     _logger.LogInformation($"抓取 {files.Count} 个贴纸耗时 {(end - start).TotalSeconds:F3} 秒");
    //
    //     return new Dictionary<string, object>
    //     {
    //         { "name", res["result"]["name"].ToString().ToLower() },
    //         { "title", res["result"]["title"].ToString() },
    //         { "files", files },
    //         { "folders", folders },
    //         { "total_count", files.Count }
    //     };
    // }
    //
    // private async Task<int> DownloadSticker(string path, string link)
    // {
    //     var response = await _httpClient.GetByteArrayAsync(link);
    //     await File.WriteAllBytesAsync(path, response);
    //     return response.Length;
    // }
    //
    // public async Task<bool> DownloadPack(Dictionary<string, object> pack, bool forceRedownload = false)
    // {
    //     var swd = Path.Combine(_cwd, pack["name"].ToString());
    //     if (forceRedownload && Directory.Exists(swd))
    //     {
    //         Directory.Delete(swd, true);
    //     }
    //
    //     Directory.CreateDirectory(swd);
    //     _logger.LogInformation($"开始下载贴纸包 \"{pack["name"]}\" 到 {swd}");
    //     var start = DateTime.Now;
    //     var files = (List<Sticker>)pack["files"];
    //     var semaphore = new SemaphoreSlim(_threads);
    //     var tasks = new List<Task<int>>();
    //     foreach (var sticker in files)
    //     {
    //         await semaphore.WaitAsync();
    //         tasks.Add(Task.Run(async () =>
    //         {
    //             try
    //             {
    //                 var filePath = Path.Combine(swd, sticker.FileType,
    //                     $"{Path.GetFileNameWithoutExtension(sticker.Name).Split('_').Last()}+{sticker.EmojiName()}.{sticker.FileType}");
    //                 Directory.CreateDirectory(Path.GetDirectoryName(filePath));
    //                 return await DownloadSticker(filePath, sticker.Link);
    //             }
    //             finally
    //             {
    //                 semaphore.Release();
    //             }
    //         }));
    //     }
    //
    //     var results = await Task.WhenAll(tasks);
    //     var downloads = results.Count(r => r > 0);
    //     var end = DateTime.Now;
    //     _logger.LogInformation($"已下载 {downloads} 个贴纸，耗时 {(end - start).TotalSeconds:F3} 秒");
    //     return downloads == files.Count;
    // }
}