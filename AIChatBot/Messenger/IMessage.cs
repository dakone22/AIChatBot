using System;
using System.Collections.Generic;

namespace AIChatBot.Messenger;

public interface IMessage
{
    IChannel Channel { get; }
    IUser Author { get; }
    string Content { get; }
    IReadOnlyCollection<IAttachment> Attachments { get; }
    DateTimeOffset Timestamp { get; }
}