// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using TelegramBots.Framework;
using TelegramBots.Framework.Extensions;
using TelegramBots.Services;
using TelegramBots.Utilities;

var builder = FluentConsoleApp.CreateBuilder(args);
var app = builder.Build();
await app.Services.BuildServiceProvider().ValidateStickerDownloaderAsync();
await app.Run<MainService>();

Console.Read();