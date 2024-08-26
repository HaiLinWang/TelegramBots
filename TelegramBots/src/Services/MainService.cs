using Dumpify;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBots.Entities;
using TelegramBots.Utilities;

namespace TelegramBots.Services
{
    public class MainService : IService
    {
        private readonly ILogger<MainService> _logger;
        private readonly AppSettings _settings;
        private readonly IConfiguration _conf;
        private readonly ITelegramBotClient _botClient;
        private readonly ITelegramBotService _telegramBotService;
        public MainService(ILogger<MainService> logger, IOptions<AppSettings> options, IConfiguration conf,ITelegramBotClient botClient, ITelegramBotService telegramBotService)
        {
            _logger = logger;
            _settings = options.Value;
            _conf = conf;
            _botClient = botClient;
            _telegramBotService = telegramBotService;
        }
        public async Task<Result> Run()
        {
            // _telegramBotService.Run();
            _logger.LogInformation("启动！");
            using var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };
            _botClient.StartReceiving(
                updateHandler: _telegramBotService.HandleUpdateAsync,
                errorHandler: _telegramBotService.HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
            var me = await _botClient.GetMeAsync();
            Console.WriteLine($"开始监听 @{me.Username} 的消息");
            Console.ReadLine();
            cts.Cancel();
            return Result.Ok();
        }
        
    }
}