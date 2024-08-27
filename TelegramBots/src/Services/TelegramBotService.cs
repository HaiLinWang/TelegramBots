using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBots.Entities;
using TelegramBots.Utilities;
using File = System.IO.File;

namespace TelegramBots.Services;

public interface ITelegramBotService
{
    Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken);

    Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken);
}

public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly AppSettings _settings;
    // private readonly IDownloaderService _downloaderService;
    private readonly FormatConvert _formatConvert;
    private ConcurrentDictionary<long, string> _userStates = new();

    public TelegramBotService(ILogger<TelegramBotService> logger, IOptions<AppSettings> options,
        ITelegramBotClient botClient,
         FormatConvert formatConvert)
    {
        _botClient = botClient;
        _logger = logger;
        _settings = options.Value;
        _formatConvert = formatConvert;
    }


    //监听
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                await HandleMessageAsync(update.Message, cancellationToken);
                break;
            case UpdateType.CallbackQuery:
                await HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
                break;
        }
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        _logger.LogInformation($"接收用户发送信息");
        // 处理文本消息
        if (message.Text is { } messageText)
        {
        // 处理其他命令
            if (messageText.StartsWith("/"))
            {
                await HandleCommandAsync(chatId, messageText, cancellationToken);
                return;
            }

            // 处理贴纸转换
            if (_userStates.TryGetValue(chatId, out var state) && state == "waiting_for_sticker_link")
            {
                await HandleStickerConversionAsync(chatId, messageText, cancellationToken);
                return;
            }

            if (_userStates.TryGetValue(chatId, out var test) && test == "waiting_test_button")
            {
                _userStates.TryRemove(chatId, out _);
                return;
            }
        }

        // 处理图片下载
        if (_userStates.TryGetValue(chatId, out var downloadState) && downloadState == "waiting_download_button")
        {
            if (message.Photo != null || message.Document != null)
            {
                await HandleDownLoadAsync(chatId, message, cancellationToken);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "请发送一张图片或文档进行下载。", cancellationToken: cancellationToken);
            }

            return;
        }
    }


    private async Task HandleCommandAsync(long chatId, string command, CancellationToken cancellationToken)
    {
        switch (command.ToLower())
        {
            case "/start":
                await ShowWelcomeMessageAsync(chatId, cancellationToken);
                break;
            case "/end":
                await ShowMainMenuAsync(chatId, cancellationToken);
                break;
            case "/help":
                await ShowHelpMessageAsync(chatId, cancellationToken);
                break;
            default:
                await ShowUnknownCommandMessageAsync(chatId, cancellationToken);
                break;
        }

        _userStates = new ConcurrentDictionary<long, string>();
    }

    private async Task ShowWelcomeMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        var welcomeMessage = "欢迎使用我们的机器人！\n\n" +
                             "可用的命令：\n" +
                             "/start - 显示菜单\n" +
                             "/end - 结束当前指令，返回菜单列表\n" +
                             "/help - 显示帮助信息";

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: welcomeMessage,
            cancellationToken: cancellationToken);
        await ShowMainMenuAsync(chatId, cancellationToken);
    }

    private async Task ShowHelpMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        var helpMessage = "这是帮助信息：\n\n" +
                          "1. 使用 /start 显示菜单\n" +
                          "2. 使用 /end 结束当前指令，返回菜单列表\n" +
                          "3. 贴纸转换，输入贴纸链接，会自动下载贴纸到download/贴纸名称 文件夹。同时会将贴纸转换为gif格式\n" +
                          "4. 图片下载转换，输入图片，会自动下载图片到download/DownLoad 文件夹。同时会将图片转换为gif格式\n" +
                          "99. 如有其他问题，请联系管理员";

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: helpMessage,
            cancellationToken: cancellationToken);
    }

    private async Task ShowUnknownCommandMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "未知命令。请使用 /help 查看可用命令列表。",
            cancellationToken: cancellationToken);
    }

    private async Task HandleOtherMessagesAsync(long chatId, string messageText, CancellationToken cancellationToken)
    {
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "如果您需要帮助，请使用 /help 命令查看可用选项。",
            cancellationToken: cancellationToken);
    }

    private async Task ShowMainMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        _userStates = new ConcurrentDictionary<long, string>();
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("贴纸转换", "sticker_conversion"),
                InlineKeyboardButton.WithCallbackData("Gif/图片下载转换", "download_button"),
                InlineKeyboardButton.WithCallbackData("测试按钮", "test_button"),
                // 可以添加更多按钮
            }
        });

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "请选择一个选项：",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 根据按钮进行操作
    /// </summary>
    /// <param name="callbackQuery"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        switch (callbackQuery.Data)
        {
            case "sticker_conversion":
                _userStates[chatId] = "waiting_for_sticker_link";
                await _botClient.SendTextMessageAsync(chatId, "请发送贴纸链接。", cancellationToken: cancellationToken);
                break;
            case "test_button":
                _userStates[chatId] = "waiting_test_button";
                await _botClient.SendTextMessageAsync(chatId, "我只是个测试按钮。", cancellationToken: cancellationToken);
                break;
            case "download_button":
                _userStates[chatId] = "waiting_download_button";
                await _botClient.SendTextMessageAsync(chatId, "请发送图片。", cancellationToken: cancellationToken);
                break;
            // 可以在这里添加其他菜单选项的处理
            default:
                await _botClient.SendTextMessageAsync(chatId, "未知的选项，请重新选择。", cancellationToken: cancellationToken);
                break;
        }

        // 回应回调查询
        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    // 下载
    private async Task HandleDownLoadAsync(long chatId, Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            string fileId = null;
            string fileName = null;
            // 创建对应的gif文件夹
            string dirFolder = Path.Combine(_settings.DownloadPath, "downLoadFiles");
            // 如果 gif 文件夹不存在，则创建
            if (!Directory.Exists(dirFolder))
            {
                Directory.CreateDirectory(dirFolder);
                _logger.LogInformation($"已创建 GIF 文件夹: {dirFolder}");
            }
            if (message.Photo != null && message.Photo.Length > 0)
            {
                // 获取最大尺寸的图片
                var photo = message.Photo.OrderByDescending(p => p.Width * p.Height).First();
                fileId = photo.FileId;
                fileName = $"photo_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            }
            else if (message.Document != null)
            {
                fileId = message.Document.FileId;
                string originalFileName = message.Document.FileName ?? $"document_{DateTime.Now:yyyyMMddHHmmss}";
                string fileExtension = Path.GetExtension(originalFileName);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
                // 组合文件名和文件ID
                fileName = $"{fileNameWithoutExtension}_{fileId}{fileExtension}";
            }

            if (fileId != null)
            {
                var file = await _botClient.GetFileAsync(fileId, cancellationToken);
                string filePath = $"{dirFolder}/{fileName}";
                if (!File.Exists(filePath))
                {
                    using (var saveImageStream = System.IO.File.Open(filePath, FileMode.Create))
                    {
                        await _botClient.DownloadFileAsync(file.FilePath, saveImageStream, cancellationToken);
                    }
                }
                // 进行文件转换
                await _formatConvert.ProcessConvertFilesAsync(filePath,1);
                await _botClient.SendTextMessageAsync(chatId, $"文件已成功下载并转换: {fileName}",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "无法处理此类型的消息。请发送图片或文档。",
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _botClient.SendTextMessageAsync(chatId, $"下载过程中出现错误: {ex.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStickerConversionAsync(long chatId, string stickerLink,
        CancellationToken cancellationToken)
    {
        try
        {
            // 验证链接格式
            if (!Uri.TryCreate(stickerLink, UriKind.Absolute, out Uri uri) ||
                (!uri.Host.Contains("telegram.me") && !uri.Host.Contains("t.me")))
            {
                await _botClient.SendTextMessageAsync(chatId, "请提供有效的 Telegram 贴纸链接。",
                    cancellationToken: cancellationToken);
                return;
            }
            // 发送"处理中"消息
            await _botClient.SendTextMessageAsync(chatId, "正在处理贴纸集，请稍候...", cancellationToken: cancellationToken);
            // 从链接中提取贴纸集名称
            string stickerSetName = uri.Segments.Last().TrimEnd('/');
            // 获取贴纸集信息
            var stickerSet = await _botClient.GetStickerSetAsync(stickerSetName, cancellationToken);
            int stickerCount = stickerSet.Stickers.Length;
            // 设置保存路径
            var swd = Path.Combine(_settings.DownloadPath, stickerSetName);
            var originalFileDir = Path.Combine(swd, "originalFiles");
            Directory.CreateDirectory(originalFileDir);
            await _botClient.SendTextMessageAsync(chatId, $"贴纸集 '{stickerSetName}' 包含 {stickerCount} 个贴纸。开始下载...",
                cancellationToken: cancellationToken);
            var semaphore = new SemaphoreSlim(_settings.DownloaderThreads);
            var tasks = new List<Task>();
            int downloadedCount = 0;
            int processedCount = 0;
            for (int i = 0; i < stickerCount; i++)
            {
                var sticker = stickerSet.Stickers[i];
                var index = i;
                await semaphore.WaitAsync(cancellationToken);
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var file = await _botClient.GetFileAsync(sticker.FileId, cancellationToken);
                        string fileExtension = sticker.IsAnimated ? "tgs" : (sticker.IsVideo ? "webm" : "webp");
                        string fileName = $"sticker_{index + 1}_{sticker.Emoji}.{fileExtension}";
                        string filePath = Path.Combine(originalFileDir, fileName);
                        bool downloaded = false;
                        if (!File.Exists(filePath))
                        {
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await _botClient.DownloadFileAsync(file.FilePath, fileStream, cancellationToken);
                            }
                            downloaded = true;
                        }
                        Interlocked.Increment(ref processedCount);
                        if (downloaded)
                        {
                            Interlocked.Increment(ref downloadedCount);
                        }
                        // 每处理5个贴纸或处理完最后一个贴纸时，更新进度
                        if (processedCount % 5 == 0 || processedCount == stickerCount)
                        {
                            await _botClient.SendTextMessageAsync(chatId, 
                                $"已处理 {processedCount} 个贴纸，下载 {downloadedCount} 个，共 {stickerCount} 个...", 
                                cancellationToken: cancellationToken);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                tasks.Add(task);
            }
            // 等待所有任务完成
            await Task.WhenAll(tasks);
            // 提示用户下载完成
            await _botClient.SendTextMessageAsync(chatId, 
                $"贴纸集 '{stickerSetName}' 开始转换gif格式到 gifs 文件夹。\n" , 
                cancellationToken: cancellationToken);
            // 进行文件转换
            await _formatConvert.ProcessConvertFilesAsync(originalFileDir);
            // 提示用户下载完成
            await _botClient.SendTextMessageAsync(chatId, 
                $"贴纸集 '{stickerSetName}' 已成功处理。新下载并转换 {downloadedCount} 个贴纸到 {swd} 文件夹。\n" +
                "您可以继续发送其他贴纸链接，或者输入 /end 来结束。", 
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            await _botClient.SendTextMessageAsync(chatId,
                $"处理贴纸集时出现错误: {ex.Message}\n您可以尝试其他贴纸链接，或输入 /end 来结束。",
                cancellationToken: cancellationToken);
        }
    }


    // 处理轮询错误
    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API 错误:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    #region  下载贴纸http请求方法
   // // 贴纸转换
    // private async Task HandleStickerConversionAsync(long chatId, string messageText,
    //     CancellationToken cancellationToken)
    // {
    //     if (!IsValidStickerLink(messageText))
    //     {
    //         await _botClient.SendTextMessageAsync(chatId, "请发送有效的贴纸链接。", cancellationToken: cancellationToken);
    //         return;
    //     }
    //
    //     await _botClient.SendTextMessageAsync(chatId, "正在解析贴纸包内容...", cancellationToken: cancellationToken);
    //     //提取链接，进行贴纸下载
    //     var url = CommonHelper.GetLastPartOfUrl(messageText);
    //     var packInfo = await _downloaderService.GetPack(url);
    //     if (packInfo == null)
    //     {
    //         await _botClient.SendTextMessageAsync(chatId, "无法获取贴纸包信息，请检查链接是否正确。", cancellationToken: cancellationToken);
    //         return;
    //     }
    //     var packName = packInfo["name"].ToString();
    //     var downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads", packName);
    //     // 获取文件夹列表
    //     var folders = ((List<string>)packInfo["folders"]).Select(folder => Path.Combine(downloadPath, folder)).ToList();
    //     bool needDownload = true;
    //     bool forceRedownload = false;
    //     // 检查是否已经下载过
    //     if (Directory.Exists(downloadPath))
    //     {
    //         var totalFiles = 0;
    //         foreach (var folder in folders)
    //         {
    //             if (Directory.Exists(folder))
    //             {
    //                 totalFiles += Directory.GetFiles(folder, $"*.{Path.GetFileName(folder)}").Length;
    //             }
    //         }
    //
    //         if (totalFiles == (int)packInfo["total_count"])
    //         {
    //             await _botClient.SendTextMessageAsync(chatId, "该贴纸包已经下载过了，正在进行转换...",
    //                 cancellationToken: cancellationToken);
    //             needDownload = false;
    //         }
    //         else
    //         {
    //             await _botClient.SendTextMessageAsync(chatId, "发现不完整的下载，正在重新下载...",
    //                 cancellationToken: cancellationToken);
    //             forceRedownload = true;
    //         }
    //     }
    //     else
    //     {
    //         await _botClient.SendTextMessageAsync(chatId, "正在下载贴纸包...", cancellationToken: cancellationToken);
    //     }
    //
    //     if (needDownload)
    //     {
    //         var isAllDown = await _downloaderService.DownloadPack(packInfo, forceRedownload);
    //         if (!isAllDown)
    //         {
    //             await _botClient.SendTextMessageAsync(chatId, "贴纸下载失败，请稍后重试。", cancellationToken: cancellationToken);
    //             return;
    //         }
    //         else
    //         {
    //             await _botClient.SendTextMessageAsync(chatId, "贴纸下载完成,正在转换格式...", cancellationToken: cancellationToken);
    //         }
    //     }
    //
    //     // 进行文件转换
    //     foreach (var folder in folders)
    //     {
    //         if (Directory.Exists(folder))
    //         {
    //             await _formatConvert.ProcessConvertFilesAsync(folder);
    //         }
    //     }
    //
    //     await _botClient.SendTextMessageAsync(chatId, "贴纸转换完成。", cancellationToken: cancellationToken);
    // }

 
    private bool IsValidStickerLink(string link)
    {
        // 这个正则表达式匹配 Telegram 贴纸链接的格式
        // 例如：https://t.me/addstickers/YourStickerSetName
        var regex = new Regex(@"^https?:\/\/t\.me\/addstickers\/[a-zA-Z0-9_]+$");
        return regex.IsMatch(link);
    }
    #endregion
}