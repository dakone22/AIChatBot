using System.Collections.Generic;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace AIChatBot.Generators.Picture;

public interface IPictureGenerator
{
    public Task<List<Image>> Generate(string prompt, string negativePrompt="");
}