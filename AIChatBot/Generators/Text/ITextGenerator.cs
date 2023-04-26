using System.Threading.Tasks;

namespace AIChatBot.Generators.Text;

public interface ITextGenerator
{
    public Task<string> GenerateText(string prompt);
}