using System.Threading.Tasks;
using Discord;

namespace AIChatBot.Discord;

public interface IDiscordBot
{
    Task StartAsync(TokenType tokenType, string token);
}