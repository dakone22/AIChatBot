namespace AIChatBot.Messenger.Discord;

public struct DiscordAttachment : IAttachment
{
    public DiscordAttachment(global::Discord.IAttachment attachment)
    {
        Id = attachment.Id;
        Filename = attachment.Filename;
        Url = attachment.Url;
        ProxyUrl = attachment.ProxyUrl;
        Size = attachment.Size;
        Height = attachment.Height;
        Width = attachment.Width;
        Ephemeral = attachment.Ephemeral;
        Description = attachment.Description;
        ContentType = attachment.ContentType;
    }

    public ulong Id { get; }
    public string Filename { get; }
    public string Url { get; }
    public string ProxyUrl { get; }
    public int Size { get; }
    public int? Height { get; }
    public int? Width { get; }
    public bool Ephemeral { get; }
    public string Description { get; }
    public string ContentType { get; }
}