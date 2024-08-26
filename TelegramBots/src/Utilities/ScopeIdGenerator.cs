namespace TelegramBots.Utilities;

public static class ScopeIdGenerator
{
    private static int _lastId = 0;
    public static int GetNextId() => Interlocked.Increment(ref _lastId);
}
