using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIChatBot.Messenger;

public interface IMessenger
{
    event Func<IMessage, Task> MessageReceivedEvent;
    event Action<string> NameUpdatedEvent;
    void SetTyping(bool isTyping);
    IEnumerable<IMessage> GetLastMessages(int limit);
    void Reply(IMessage repliedMessage, string reply);
}

//private async Task<IEnumerable<string>> GetOtherMessages(SocketMessage socketMessage, int limit = 100)
//{
//    var messagesEnumerator = await socketMessage.Channel.GetMessagesAsync(limit).FlattenAsync();
//    var messages = messagesEnumerator as IMessage[] ?? messagesEnumerator.ToArray();
//
//    var messageAuthorIds = messages
//        .Select(message => message.Author.Id)
//        .ToHashSet();
//
//    var userMap = _server.Users
//        .Where(socketGuildUser => messageAuthorIds.Contains(socketGuildUser.Id))
//        .ToDictionary(socketGuildUser => socketGuildUser.Id);
//
//    var chatHistoryEntries = messages
//        .Select(message =>
//        {
//            if (!userMap.TryGetValue(message.Author.Id, out var messageUser)) return null;
//
//            var messageUserName = messageUser.Nickname ?? messageUser.Username;
//            var messageContent = message.Content;
//            var isImagePresent = message.Attachments.Count > 0;
//            var chatHistoryEntryFormat = isImagePresent ? "[{0}]: {1}" : "[{0}]: <attachment.jpg> {1}";
//
//            return string.Format(chatHistoryEntryFormat, messageUserName, messageContent);
//        })
//        .Where(chatHistoryEntry => chatHistoryEntry != null);
//    return chatHistoryEntries;
//}