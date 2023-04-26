using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using SixLabors.ImageSharp;

namespace AIChatBot.Generators.Picture.StableDiffusion;

public interface IApiFormat
{
    string GetContentType();
    object Serialize(object parameters);
    List<Image> GetAnswer(string responseText);
}

public interface IApiMethod
{
    Task<string> GetResponseText(IApiFormat apiFormat, string apiEndpointUrl, object parameters);
}

public class JsonApiFormat : IApiFormat
{
    public string GetContentType()
    {
        return "application/json";
    }

    public object Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public List<Image> GetAnswer(string responseText)
    {
        var jsonResponse = JObject.Parse(responseText);
        var images = jsonResponse["images"].ToObject<JArray>();
        
        // ...
        return null;
    }
}

public class RestApiMethod : IApiMethod
{
    public async Task<string> GetResponseText(IApiFormat apiFormat, string apiEndpointUrl, object parameters)
    {
        var client = new RestClient(apiEndpointUrl);
        var request = new RestRequest(apiEndpointUrl, Method.Post);

        request.AddHeader("Content-Type", apiFormat.GetContentType());
        request.AddParameter(apiFormat.GetContentType(), apiFormat.Serialize(parameters), ParameterType.RequestBody);

        var response = await client.ExecuteAsync(request);
        
        return response.Content;
    }
}

internal interface ITextGen
{
    public async Task<List<Image>> Ask(string apiEndpointUrl, IApiFormat apiFormat, IApiMethod apiMethod, string prompt)
    {
        var parameters = GetParameters(prompt);

        var responseText = await apiMethod.GetResponseText(apiFormat, apiEndpointUrl, parameters);

        return apiFormat.GetAnswer(responseText);
    }

    public object GetParameters(string prompt);
}


public class PictureGenerator : IPictureGenerator
{
    private readonly string _url;
    
    public PictureGenerator(string url)
    {
        _url = url;
    }

    public async Task<List<Image>> Generate(string prompt, string negativePrompt = "")
    {
        var payload = GetParameters(prompt, negativePrompt).ToString();
        
        var url = $"{_url}/sdapi/v1/txt2img";
        
        var client = new RestClient(url);
        var request = new RestRequest(url, Method.Post);

        request.AddHeader("Content-Type", "application/json");
        request.AddParameter("application/json", payload, ParameterType.RequestBody);

        var response = await client.ExecuteAsync(request);
        if (!response.IsSuccessful) {
            throw new Exception("Request failed: " + response.ErrorMessage);
        }

        var jsonResponse = JObject.Parse(response.Content);
        var images = jsonResponse["images"].ToObject<JArray>();

        var result = new List<Image>(images.Count);
        
        foreach (var imageBase64 in images) {
            //string base64 = imageBase64.ToString().Split(",", 2)[1];
            var imageData = imageBase64.ToString();
            var commaIndex = imageData.IndexOf(',') + 1;
            var base64 = imageData[commaIndex..];

            // Decode the base64 string to an image
            using var imageStream = new MemoryStream(Convert.FromBase64String(base64));
            var image = await Image.LoadAsync(imageStream);
            
            result.Add(image);
        }

        return result;
    }

    private static JObject GetParameters(string prompt, string negativePrompt)
    {
        var payload = new JObject {
            { "prompt", prompt },
            { "negative_prompt", negativePrompt },
            { "steps", 20 },
            { "height", 688 },
            { "send_images", true },
            { "sampler_name", "DDIM" },
            { "filter_nsfw", false }
        };
        return payload;

        // full list:
        // "enable_hr": false,
        // "denoising_strength": 0,
        // "firstphase_width": 0,
        // "firstphase_height": 0,
        // "hr_scale": 2,
        // "hr_upscaler": "string",
        // "hr_second_pass_steps": 0,
        // "hr_resize_x": 0,
        // "hr_resize_y": 0,
        // "prompt": "",
        // "styles": [
        //   "string"
        // ],
        // "seed": -1,
        // "subseed": -1,
        // "subseed_strength": 0,
        // "seed_resize_from_h": -1,
        // "seed_resize_from_w": -1,
        // "sampler_name": "string",
        // "batch_size": 1,
        // "n_iter": 1,
        // "steps": 50,
        // "cfg_scale": 7,
        // "width": 512,
        // "height": 512,
        // "restore_faces": false,
        // "tiling": false,
        // "do_not_save_samples": false,
        // "do_not_save_grid": false,
        // "negative_prompt": "string",
        // "eta": 0,
        // "s_churn": 0,
        // "s_tmax": 0,
        // "s_tmin": 0,
        // "s_noise": 1,
        // "override_settings": { },
        // "override_settings_restore_afterwards": true,
        // "script_args": [],
        // "sampler_index": "Euler",
        // "script_name": "string",
        // "send_images": true,
        // "save_images": false,
        // "alwayson_scripts": { }
    }
}