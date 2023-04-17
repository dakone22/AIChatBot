using System.Threading.Tasks;
using AIChatBot.Discord;
using AIChatBot.Generators.Picture;
using AIChatBot.Generators.Text;
using Discord;

namespace AIChatBot;

public class AiChatBot
{
    private readonly IDiscordBot _discordBot = new DiscordBot();
    private readonly IPictureGenerator _pictureGenerator;
    private readonly ITextGenerator _textGenerator;

    private async Task StartAsync()
    {
        await _discordBot.StartAsync(TokenType.Bot, Config.BotToken);
    }
}