namespace AIChatBot.Messenger.Discord;

public struct DiscordChannel : IChannel
{
    public DiscordChannel(global::Discord.IChannel channel)
    {
        Id = channel.Id;
        Name = channel.Name;
    }

    public ulong Id { get; }
    public string Name { get; }
}