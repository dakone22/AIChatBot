using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace AIChatBot.Discord;

public class DiscordBot : IDiscordBot
{
    private readonly DiscordSocketClient _client;

    public DiscordBot() : this(new DiscordSocketConfig
    {
        MessageCacheSize = 1200,
        LogLevel = LogSeverity.Debug,
        AlwaysDownloadUsers = true,
        GatewayIntents =
            GatewayIntents.MessageContent |
            GatewayIntents.Guilds |
            GatewayIntents.GuildMessages
    })
    {
    }

    private DiscordBot(DiscordSocketConfig config)
    {
        _client = new DiscordSocketClient(config);

        // _client.Log += Client_Log;
        // _client.Ready += StartLoop;
        // _client.MessageReceived += Client_MessageReceived;
        // _client.GuildMemberUpdated += Client_GuildMemberUpdated;
    }

    public async Task StartAsync(TokenType tokenType, string token)
    {
        await _client.LoginAsync(TokenType.Bot, Config.BotToken);
        await _client.StartAsync();
    }
}