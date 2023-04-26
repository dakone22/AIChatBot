using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AIChatBot.Messenger.Discord;

public struct DiscordMessage : IMessage
{
    public DiscordMessage(global::Discord.IMessage message)
    {
        Channel = new DiscordChannel(message.Channel);
        Author = new DiscordUser(message.Author);
        Content = message.Content;
        Attachments = message.Attachments.Select<global::Discord.IAttachment, Messenger.IAttachment>(attachment => new DiscordAttachment(attachment)).ToImmutableArray();
        Timestamp = message.Timestamp;
    }
    public IChannel Channel { get; }
    public IUser Author { get; }
    public string Content { get; }
    public IReadOnlyCollection<Messenger.IAttachment> Attachments { get; }
    public DateTimeOffset Timestamp { get; }
}