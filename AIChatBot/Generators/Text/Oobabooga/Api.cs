using System.Net.Http;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace AIChatBot.Generators.Text.Oobabooga;

public interface IApi
{
    string GetUrl(string url);

    HttpContent GetRequestContent(string prompt);

    string GetAnswer(string responseText);
}

internal interface IJsonApi : IApi
{
    public object GetContentObject(string prompt);

    public JsonElement GetAnswerJsonElement(JsonDocument jsonDocument);
    
    HttpContent IApi.GetRequestContent(string prompt)
    {
        return new StringContent(
            JsonConvert.SerializeObject(GetContentObject(prompt)),
            Encoding.UTF8,
            "application/json"
        );
    }

    string IApi.GetAnswer(string responseText)
    {
        var jsonDocument = JsonDocument.Parse(responseText);
        var jsonElement = GetAnswerJsonElement(jsonDocument);
        return jsonElement.ToString();
    }
}

/// <summary>
/// try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
/// new better API, use this with the oob arg --extensions api
/// </summary>
public class NewApi : IJsonApi
{
    public string GetUrl(string url)
    {
        return url + "/api/v1/generate";
    }

    public object GetContentObject(string prompt)
    {
        return new Parameters(prompt);
    }
    
    public JsonElement GetAnswerJsonElement(JsonDocument jsonDocument)
    {
        return jsonDocument.RootElement.GetProperty("results")[0].GetProperty("text");
    }
}

/// <summary>
/// old default API (busted but it kinda works)
/// </summary>
public class OldApi : IJsonApi
{
    public string GetUrl(string url)
    {
        return url + "/run/textgen";
    }

    public object GetContentObject(string prompt)
    {
        var payload = JsonConvert.SerializeObject(new object[] { prompt, new Parameters(prompt)});
        return new { data = new[] { payload } };
    }

    public JsonElement GetAnswerJsonElement(JsonDocument jsonDocument)
    {
        return jsonDocument.RootElement.GetProperty("data")[0];
    }
}