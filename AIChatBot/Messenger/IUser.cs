namespace AIChatBot.Messenger;

public interface IUser
{
    ulong Id { get; }
    string Name { get; }
}