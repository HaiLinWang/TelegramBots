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
    private readonly IStickerDownloaderService _stickerDownloaderService;
    private readonly FormatConvert _formatConvert;
    private readonly ConcurrentDictionary<long, string> _userStates = new();

    public TelegramBotService(ILogger<TelegramBotService> logger, ITelegramBotClient botClient,
        IStickerDownloaderService stickerDownloaderService, FormatConvert formatConvert)
    {
        _botClient = botClient;
        _logger = logger;
        _stickerDownloaderService = stickerDownloaderService;
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

    /// <summary>
    /// 根据start启动机器人
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is not { } messageText)
            return;
        var chatId = message.Chat.Id;
        // 处理命令
        if (messageText.StartsWith("/"))
        {
            await HandleCommandAsync(chatId, messageText, cancellationToken);
            return;
        }

        //处理贴纸转换
        if (_userStates.TryGetValue(chatId, out var sticker) && sticker == "waiting_for_sticker_link")
        {
            await HandleStickerConversionAsync(chatId, messageText, cancellationToken);
            _userStates.TryRemove(chatId, out _);
        }

        if (_userStates.TryGetValue(chatId, out var test) && test == "waiting_test_button")
        {
            _userStates.TryRemove(chatId, out _);
        }
        // else
        // {
        //     // 处理其他消息或显示主菜单
        //     await ShowMainMenuAsync(chatId, cancellationToken);
        // }
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
                          "3. 如需贴纸转换，请在主菜单中选择相应选项\n" +
                          "4. 如有其他问题，请联系管理员";

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
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("贴纸转换", "sticker_conversion"),
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
            // 可以在这里添加其他菜单选项的处理
            default:
                await _botClient.SendTextMessageAsync(chatId, "未知的选项，请重新选择。", cancellationToken: cancellationToken);
                break;
        }

        // 回应回调查询
        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    // 贴纸转换
    private async Task HandleStickerConversionAsync(long chatId, string messageText,
        CancellationToken cancellationToken)
    {
        if (!IsValidStickerLink(messageText))
        {
            await _botClient.SendTextMessageAsync(chatId, "请发送有效的贴纸链接。", cancellationToken: cancellationToken);
            return;
        }

        await _botClient.SendTextMessageAsync(chatId, "正在解析贴纸包内容...", cancellationToken: cancellationToken);
        //提取链接，进行贴纸下载
        var url = CommonHelper.GetLastPartOfUrl(messageText);
        var packInfo = await _stickerDownloaderService.GetPack(url);
        if (packInfo == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "无法获取贴纸包信息，请检查链接是否正确。", cancellationToken: cancellationToken);
            return;
        }

        var packName = packInfo["name"].ToString();
        var downloadPath = Path.Combine(Directory.GetCurrentDirectory(), "downloads", packName);
        // 获取文件夹列表
        var folders = ((List<string>)packInfo["folders"]).Select(folder => Path.Combine(downloadPath, folder)).ToList();
        bool needDownload = true;
        bool forceRedownload = false;
        // 检查是否已经下载过
        if (Directory.Exists(downloadPath))
        {
            var totalFiles = 0;
            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    totalFiles += Directory.GetFiles(folder, $"*.{Path.GetFileName(folder)}").Length;
                }
            }

            if (totalFiles == (int)packInfo["total_count"])
            {
                await _botClient.SendTextMessageAsync(chatId, "该贴纸包已经下载过了，正在进行转换...",
                    cancellationToken: cancellationToken);
                needDownload = false;
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "发现不完整的下载，正在重新下载...",
                    cancellationToken: cancellationToken);
                forceRedownload = true;
            }
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, "正在下载贴纸包...", cancellationToken: cancellationToken);
        }

        if (needDownload)
        {
            var isAllDown = await _stickerDownloaderService.DownloadPack(packInfo, forceRedownload);
            if (!isAllDown)
            {
                await _botClient.SendTextMessageAsync(chatId, "贴纸下载失败，请稍后重试。", cancellationToken: cancellationToken);
                return;
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "贴纸下载完成,正在转换格式...", cancellationToken: cancellationToken);
            }
        }

        // 进行文件转换
        foreach (var folder in folders)
        {
            if (Directory.Exists(folder))
            {
                await _formatConvert.ProcessConvertFilesAsync(folder);
            }
        }

        await _botClient.SendTextMessageAsync(chatId, "贴纸转换完成。", cancellationToken: cancellationToken);
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

    private bool IsValidStickerLink(string link)
    {
        // 这个正则表达式匹配 Telegram 贴纸链接的格式
        // 例如：https://t.me/addstickers/YourStickerSetName
        var regex = new Regex(@"^https?:\/\/t\.me\/addstickers\/[a-zA-Z0-9_]+$");
        return regex.IsMatch(link);
    }
}