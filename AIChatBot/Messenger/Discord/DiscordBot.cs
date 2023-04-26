using System;
using System.Collections.Generic;
using System.Regex;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace AIChatBot.Messenger.Discord;

public class DiscordBot : IMessenger, IDisposable
{
    private readonly ILogger<DiscordBot> _logger;
    private readonly DiscordSocketClient _client;
    private SocketGuild _server;

    private ulong _botUserId;
    private string _botName;
    
    public void Dispose() {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public DiscordBot(ILogger<DiscordBot> logger) : this(logger, new DiscordSocketConfig
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

    private DiscordBot(ILogger<DiscordBot> logger, DiscordSocketConfig config)
    {
        _logger = logger;
        _client = new DiscordSocketClient(config);

        _client.Ready += OnReady;
        _client.Log += OnLog;
        _client.MessageReceived += async message => await MessageReceivedEvent?.Invoke(new DiscordMessage(message))!;
        _client.GuildMemberUpdated += OnGuildMemberUpdated;
    }

    public async Task StartAsync(TokenType tokenType, string token)
    {
        try
        {
            await _client.LoginAsync(tokenType, token);
            await _client.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start DiscordBot");
            throw;
        }
    }

    private async Task OnReady()
    {
        _server = _client.GetGuild(Config.GuildId);

        if (_server == null) {
            throw new Exception("The bot is not a member of the specified guild.");
        }

        while (string.IsNullOrEmpty(_server.Name)) {
            Console.WriteLine("| Waiting for connection to be established by Discord...");
            await Task.Delay(1200);
        }
        
        Console.WriteLine($"| Server detected: {_server.Name}");
        
        _botUserId = _client.CurrentUser.Id;
        var botUser = _server.GetUser(_botUserId);
        _botName = botUser.Nickname ?? botUser.Username;
    }

    private Task OnLog(LogMessage msg)
    {
        const string pattern = @"(PRESENCE_UPDATE|TYPING_START|MESSAGE_(CREATE|DELETE|UPDATE)|CHANNEL_UPDATE|GUILD_|REACTION_|VOICE_STATE_UPDATE|DELETE channels/|POST channels/|Heartbeat|GET |PUT |Latency = |handler is blocking the)";
        
        if (msg.Exception != null)
        {
            _logger.LogError(msg.Exception, "{Source}: {ExceptionMessage}", msg.Source, msg.Exception.Message);
        }
        else if (msg.Message != null && !Regex.IsMatch(msg.Message, pattern))
        {
            _logger.LogInformation("{Source}: {Message}", msg.Source, msg.Message);
        }
        
        return Task.CompletedTask;
    }

    private Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> oldUserCache, SocketGuildUser newUser)
    {
        // Check if the updated user is the bot itself
        if (oldUserCache.Value.Id != _botUserId) 
            return Task.CompletedTask;

        var newBotName = newUser.Nickname ?? newUser.Username;

        // If the bot's username or nickname has changed, update the stored bot name
        _botName = newBotName;
        
        NameUpdatedEvent?.Invoke(_botName);

        return Task.CompletedTask;
    }

    public void SetTyping(bool isTyping)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IMessage> GetLastMessages(int limit)
    {
        throw new NotImplementedException();
    }

    public event Func<IMessage, Task> MessageReceivedEvent;
    public event Action<string> NameUpdatedEvent;

    public void Reply(IMessage repliedMessage, string reply)
    {
        throw new NotImplementedException();
    }
}