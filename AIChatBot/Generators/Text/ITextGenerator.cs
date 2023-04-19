using System.Threading.Tasks;

namespace AIChatBot.Generators.Text;

public interface ITextGenerator
{
    public Task<string> Ask(string prompt);
}