using System.Collections.Generic;
using System.Linq;
using AIChatBot.Messenger;
using NetCoreExtensions.Strings;

namespace AIChatBot;

public interface ITextPrompter
{
    string GeneratePrompt(IMessage onMessage);
    void SetName(string name);
}

public class SimpleTextPrompter : ITextPrompter
{
    private string _name;

    public void SetName(string name) {
        _name = name;
    }

    private static string AttachmentsToString(IEnumerable<IAttachment> attachments)
    {
        return attachments.Select(attachment => $"<{attachment.Filename}>").Join(" ");
    }

    private static string MessageToString(IMessage message)
    {
        var username = message.Author.Name;
        var content = message.Content;

        return $"[{username}]:{AttachmentsToString(message.Attachments)} {content}";
    } 

    public string GeneratePrompt(IMessage onMessage)
    {
        //_messages.Add(onMessage);
        //var historyString = _messages.Select(MessageToString).Join("\n");
        //return historyString + $"\n[{_name}]: ";
        return MessageToString(onMessage) + $"\n[{_name}]: ";
    }
}