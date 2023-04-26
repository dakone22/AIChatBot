using System.Threading.Tasks;
using AIChatBot.Generators.Picture;
using AIChatBot.Generators.Text;
using AIChatBot.Messenger;
using AIChatBot.Messenger.Discord;
using Discord;
using Microsoft.Extensions.Logging;

namespace AIChatBot;

public class AiChatBot
{
    private readonly IMessenger _messenger;
    private readonly ITextPrompter _textPrompter;
    private readonly ITextGenerator _textGenerator;
    //private readonly IPictureGenerator _pictureGenerator;

    private bool _isBusy;
    public AiChatBot()
    {
        _messenger = new DiscordBot(new Logger<DiscordBot>(new LoggerFactory()));
        _textPrompter = new SimpleTextPrompter();
    }

    private async Task StartAsync()
    {
        await ((DiscordBot)_messenger).StartAsync(TokenType.Bot, Config.BotToken);
        
        //_textPrompter.SetHistory(await _messenger.GetLastMessages(10));
        
        _messenger.NameUpdatedEvent += newName => _textPrompter.SetName(newName);
        _messenger.MessageReceivedEvent += async message =>
        {
            if (_isBusy) return;
            _isBusy = true;
            _messenger.SetTyping(true);

            var prompt = _textPrompter.GeneratePrompt(message);
            var answerText = await _textGenerator.GenerateText(prompt);
            _isBusy = false;

            _messenger.SetTyping(false);
            _messenger.Reply(message, answerText);
        };

        await Task.Delay(-1);
    }
}