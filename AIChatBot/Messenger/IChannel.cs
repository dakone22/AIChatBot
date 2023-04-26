namespace AIChatBot.Messenger;

public interface IChannel
{
    ulong Id { get; }
    string Name { get; }
}