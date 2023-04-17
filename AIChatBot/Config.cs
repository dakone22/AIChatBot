using System;

namespace AIChatBot;

internal static class Config
{
    internal static readonly string BotToken = Environment.GetEnvironmentVariable("AIChatBot.BotToken");
    internal static readonly ulong GuildId = Convert.ToUInt64(Environment.GetEnvironmentVariable("AIChatBot.GuildId"));
}
