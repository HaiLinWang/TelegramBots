using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TelegramBots.Entities;

namespace TelegramBots.Utilities
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(OutputResult))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {

    }
}