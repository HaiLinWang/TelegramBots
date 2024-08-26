using FluentResults;

namespace TelegramBots.Services
{
    public interface IService
    {
        Task<Result> Run();
    }
}