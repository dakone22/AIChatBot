using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AIChatBot.Generators.Text.Oobabooga;

// [
//     (Oobabooga("http://localhost:7861", "/run/textgen")),
//     (Oobabooga("http://localhost:7860", "/run/textgen")),
//     (Oobabooga("http://localhost:5000", "/api/v1/generate")),
//     (Dalai("http://localhost:3000"), low_priority=true),
//]

public class TextGenerator : ITextGenerator
{
    private readonly string _url;
    private readonly IApi _api;
    
    private readonly HttpClient _httpClient;
    
    public TextGenerator(string url, IApi api)
    {
        _url = url;
        _api = api;
        _httpClient = new HttpClient();
    }

    public async Task<string> Ask(string prompt)
    {

        var requestContent = _api.GetRequestContent(prompt);
        Console.WriteLine(await requestContent.ReadAsStringAsync());
        
        var response = await _httpClient.PostAsync(_api.GetUrl(_url), requestContent);
        var responseText = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseText);
        var answer = _api.GetAnswer(responseText);  // get just the response part of the json

        return answer;
    }
}