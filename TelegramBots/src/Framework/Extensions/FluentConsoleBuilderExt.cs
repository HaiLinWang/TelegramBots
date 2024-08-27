using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using TelegramBots.Entities;
using TelegramBots.Services;
using TelegramBots.Utilities;

namespace TelegramBots.Framework.Extensions
{
    public static class FluentConsoleBuilderExt
    {
        public static FluentConsoleBuilder InitializeConfiguration(this FluentConsoleBuilder builder)
        {
            IConfigurationRoot? config;
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddEnvironmentVariables();
            configBuilder.SetBasePath(Environment.CurrentDirectory);
            configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            try
            {
                config = configBuilder.Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"配置文件加载失败！请检查配置文件是不是哪里写错了？\n错误信息：{ex.Message}");
                throw;
            }

            builder.Configuration = config;
            builder.Services.AddSingleton<IConfiguration>(config);
            builder.Services.AddOptions().Configure<AppSettings>(e => config.GetSection(nameof(AppSettings)).Bind(e));
            // 注册 HttpClient
            builder.Services.AddHttpClient();
            // 注册 ITelegramBotService
            builder.Services.AddSingleton<ITelegramBotService, TelegramBotService>();
            // builder.Services.AddTransient<IStickerDownloaderService, StickerDownloaderService>();
            builder.Services.AddScoped<FormatConvert>();
            return builder;
        }

        public static FluentConsoleBuilder InitializeLogging(this FluentConsoleBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: "logs/telegramBots-.log", // 日志文件路径和名称前缀
                    rollingInterval: RollingInterval.Day, // 按天滚动
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 1073741824, // 文件大小限制：1GB
                    retainedFileCountLimit: 31, // 保留最近31天的日志文件
                    rollOnFileSizeLimit: true, // 当文件大小达到限制时，创建新文件
                    shared: true, // 允许多个进程写入同一个文件
                    flushToDiskInterval: TimeSpan.FromSeconds(1) // 每秒刷新到磁盘
                )
                .CreateLogger();

            builder.Services.AddLogging(b =>
            {
                b.AddConfiguration(builder.Configuration.GetSection("Logging"));
                b.AddConsole();
                b.AddSerilog(dispose: true);
            });

            return builder;
        }

        public static FluentConsoleBuilder RegisterServices(this FluentConsoleBuilder builder)
        {
            var serviceTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IService).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

            foreach (var type in serviceTypes)
            {
                builder.Services.AddSingleton(type);
            }

            return builder;
        }

        public static FluentConsoleBuilder AddTelegramBot(this FluentConsoleBuilder builder)
        {
            builder.Services.AddSingleton<ITelegramBotClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
                return new TelegramBotClient(settings.TelegramBotToken);
            });
            return builder;
        }

        public static async Task ValidateStickerDownloaderAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            // var stickerDownloader = scope.ServiceProvider.GetRequiredService<IStickerDownloaderService>();
            var _appSettings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>();
            var _logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                if (!Directory.Exists(_appSettings.Value.DownloadPath))
                {
                    Directory.CreateDirectory(_appSettings.Value.DownloadPath);
                    _logger.LogInformation($"成功创建下载文件夹：{_appSettings.Value.DownloadPath}");
                }
                else
                {
                    _logger.LogInformation($"下载文件夹已存在：{_appSettings.Value.DownloadPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dir 初始化失败");
            }
        }
    }
}