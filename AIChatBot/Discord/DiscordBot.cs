using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace AIChatBot.Discord;

public class DiscordBot : IDiscordBot
{
    private readonly DiscordSocketClient _client;
    private SocketGuild _server;

    private ulong _botUserId;
    private string _botName;

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

        _client.Ready += OnReady;
        _client.Log += OnLog;
        _client.MessageReceived += OnMessageReceived;
        _client.GuildMemberUpdated += OnGuildMemberUpdated;
    }

    public async Task StartAsync(TokenType tokenType, string token)
    {
        await _client.LoginAsync(TokenType.Bot, Config.BotToken);
        await _client.StartAsync();
    }

    public Task OnReady()
    {
        _server = _client.GetGuild(Config.GuildId);

        if (_server == null) { // check if bot is in a server
            throw new Exception("A server has not yet been defined"); // TODO: Exception
        }
        
        Console.WriteLine($"| Server detected: {_server.Name}");

        while (_server?.Name?.Length < 1)
        {
            Console.WriteLine("| Waiting for connection to be established by Discord...");
            Task.Delay(1200);
        }

        _botUserId = _client.CurrentUser.Id; // <--- bot's user ID is detected and filled in automatically

        _botName = _server.GetUser(_botUserId).Nickname ??  // check if there is a nickname to set
                   _server.GetUser(_botUserId).Username;    // otherwise, just use username if there is no nickname
        
        return Task.CompletedTask;
    }

    public Task OnLog(LogMessage logMessage)
    {
        throw new System.NotImplementedException();
    }

    public Task OnMessageReceived(SocketMessage socketMessage)
    {
        throw new System.NotImplementedException();
    }

    public Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser socketGuildUser)
    {
        throw new System.NotImplementedException();
    }
}