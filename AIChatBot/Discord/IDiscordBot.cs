using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace AIChatBot.Discord;

public interface IDiscordBot
{
    Task StartAsync(TokenType tokenType, string token);
    Task OnReady();
    Task OnLog(LogMessage logMessage);
    Task OnMessageReceived(SocketMessage socketMessage);
    Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser socketGuildUser);
}