using AIChatBot.Generators.Text;

namespace AIChatBot.Test;

public sealed class TextGeneratorTest
{
    private class DummyTextGenerator : ITextGenerator
    {
        public async Task<string> GenerateText(string prompt)
        {
            // thinking for 5 seconds
            await Task.Delay(5000);
            
            // answer a question
            return $"Q: {prompt}\nA: Yes.";
        }
    }

    [Fact]
    public async Task CorrectPromptsTest()
    {
        ITextGenerator textGenerator = new DummyTextGenerator();

        var prompts = new[] {
            "What is the purpose of life?",
            "2+2"
        };
        
        foreach (var prompt in prompts) {
            var answer = await textGenerator.GenerateText(prompt);
            Assert.Equal($"Q: {prompt}\nA: Yes.", answer);
        }
    }
}