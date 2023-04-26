namespace AIChatBot.Messenger.Discord;

public struct DiscordUser : IUser
{
    public DiscordUser(global::Discord.IUser user)
    {
        Id = user.Id;
        Name = user.Username;
    }


    public ulong Id { get; }
    public string Name { get; }
}