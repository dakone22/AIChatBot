using System;
using System.Threading.Tasks;
using SocketIOClient;

namespace AIChatBot.Generators.Text.Dalai;

public class TextGenerator : ITextGenerator
{
    private readonly SocketIO _socket;
    private bool _connected;

    public TextGenerator(string url)
    {
        throw new NotImplementedException("no Dalai implemented");

        _socket = new SocketIO(url);
        _socket.OnConnected += (_, _) =>
        {
            Console.WriteLine("Connected to Dalai server.");  // TODO: proper logging
            _connected = true;
        };
    }
    
    public async Task<string> Ask(string prompt)
    {
        throw new NotImplementedException("no Dalai implemented");

        if (!_connected)
            await _socket.ConnectAsync();  // TODO: another method of connection?
        
        // dalai alpaca server request
        var parameters = new
        {
            seed = -1,
            threads = 16,
            n_predict = 200,
            top_k = 40,
            top_p = 0.9,
            temp = 0.8,
            repeat_last_n = 64,
            repeat_penalty = 1.1,
            debug = false,
            model = "alpaca.7B",
            prompt
        };

        var _token = string.Empty; // clear the token string at the start of the request, ready for the Dalai server to write new tokens to it
        var llmMsg = string.Empty;
        var llmFinalMsg = string.Empty;

        var tokenStartIndex = 0;
        var tokenEndIndex = 0;
        var botMsgCount = 0;
        var botImgCount = 0;

        var listening = false;
        var imgListening = false;
        var promptEndDetected = false;
        var cursorPosition = Console.GetCursorPosition();

        // dalai
        await _socket.EmitAsync("request", parameters);

        return "";
    }
}