using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TelegramBots.Utilities;

namespace TelegramBots.Framework;

public class ScopeLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ScopeLoggingMiddleware> _logger;

    public ScopeLoggingMiddleware(RequestDelegate next, ILogger<ScopeLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var scopeId = ScopeIdGenerator.GetNextId();
        _logger.LogInformation("Scope {ScopeId} started", scopeId);

        using (var scope = _logger.BeginScope("ScopeId: {ScopeId}", scopeId))
        {
            await _next(context);
        }

        _logger.LogInformation("Scope {ScopeId} ended", scopeId);
    }
}
