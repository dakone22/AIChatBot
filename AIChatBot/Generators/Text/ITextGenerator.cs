using System.Threading.Tasks;

namespace AIChatBot.Generators.Text;

internal interface ITextGenerator
{
    public Task<string> Ask(string prompt);
}