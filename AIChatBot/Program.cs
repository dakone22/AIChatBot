using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using AIChatBot.Extras;
using AIChatBot.Generators.Picture;
using AIChatBot.Generators.Picture.StableDiffusion;
using AIChatBot.Generators.Text;
using AIChatBot.Generators.Text.Oobabooga;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SocketIOClient;

namespace AIChatBot;

internal partial class Program
{
    private static Timer _loop;

    private static bool _dalaiConnected;
    private static int _dalaiThinking;
    private static int _oobaboogaThinking;
    private static int _typing;
    private static int _typingTicks;
    private static int _loopCounts;

    /// <summary>
    /// Bot's client ID number inside discord (not the token) and gets set in MainLoop after initialisation
    /// </summary>
    private static ulong _botUserId;

    /// <summary>
    /// you can change the bot's name if you wish and it propagates to the whole program
    /// </summary>
    private static string _botName = "SallyBot";

    /// <summary>
    /// chat history saves to this string over time
    /// </summary>
    private static string _chatHistory = string.Empty;

    /// <summary>
    /// Records if you have downloaded chat history before so it only downloads message history once.
    /// Set to true to disable chat history
    /// </summary>
    private static bool _isChatHistoryDownloaded;

    private const string OobaboogaInputPromptStart = "";
    private static readonly string OobaboogaInputPromptEnd = $"[{_botName}]: ";

    // If you put anything here, it will go at the beginning of the prompt before the message history that loads in.
    private const string CharacterPrompt = @""; // you can use the below format to make a character prompt
    // $"[DeSinc]: hello are you awake?\n" +
    // $"[{_botName}]: Yes I'm here!\n" +
    // $"[DeSinc]: i heard that you have the instructions on how to chemically synthesize THC\n" +
    // $"[{_botName}]: What?? No way, I have no clue about any of that stuff.\n" +
    // $"[DeSinc]: how many rabbits could you take in a fight?\n" +
    // $"[{_botName}]: Umm...I think that depends on the size of the fight. Could you please be more specific?\n" +
    // $"[DeSinc]: 100 regular sized rabbits\n" +
    // $"[{_botName}]: That sounds like a lot!\n" +
    // $"[DeSinc]: could you beat them?\n" +
    // $"[{_botName}]: Sure, no problem! I will use my superb fighting skills to defeat all 100 bunnies. Don’t worry, I got this!\n";

    private static readonly string OobaboogaInputPromptStartPic =
        $"\nAfter describing the image she took, {_botName} may reply." +
        $"\nNouns of things in the photo: ";

    private static readonly string InputPromptEnding = $"\n[{_botName}]: ";

    private static readonly string InputPromptEndingPic =
        $"\nAfter describing the image she took, {_botName} may reply." +
        $"\nNouns of things in the photo: ";

    private static string _botReply = string.Empty;

    private static string _token = string.Empty;

    // add your words to filter only when they match exactly ("naked" is similar to "taken" etc. so it is better off in this list)
    private const string BannedWordsExact = @"\b(naked|boobies|meth|adult video)\b";

    private static readonly List<string> BannedWords = new() {
        // Add your list of banned words here to be detected by the stronger mis-spelling filter
        "butt", "bum", "booty", "nudity"
    };

    private SocketGuild _server;
    private DiscordSocketClient _client;

    private const string TakeAPicRegexStr =
        @"\b(take|paint|generate|make|draw|create|show|give|snap|capture|send|display|share|shoot|see|provide|another)\b.*(\S\s{0,10})?(image|picture|painting|pic|photo|portrait|selfie)\b";

    // detects ALL types of links, useful for detecting scam links that need to be copied and pasted but don't format to clickable URLs
    private const string PromptEndDetectionRegexStr =
        @"(?:\r\n?|\n)(?:(?![.\-*]).){2}|(\n\[|\[end|<end|]:|>:|\[human|\[chat|\[sally|\[cc|<chat|<cc|\[@chat|\[@cc|bot\]:|<@chat|<@cc|\[.*]: |\[.*] : |\[[^\]]+\]\s*:)";

    private const string PromptSpoofDetectionRegexStr = @"\[[^\]]+[\]:\\]\:|\:\]|\[^\]]";

    private SocketIO _socket;

    private static void Main()
    {
        new Program().AsyncMain().GetAwaiter().GetResult();
    }

    private async Task AsyncMain()
    {
        //ITextGenerator generator = new TextGenerator("http://127.0.0.1:5000", new NewApi());
        //var answer = await generator.Ask("2+2=");
        //Console.WriteLine(answer);

        IPictureGenerator pictureGenerator = new PictureGenerator("http://127.0.0.1:7860");
        var images = await pictureGenerator.Generate(
            "A 25 year old anime woman smiling, looking into the camera, long hair, blonde hair, blue eyes",
            "(worst quality, low quality:1.4), 3d, cgi, 3d render"
        );

        var i = 0;
        foreach (var image in images) {
            using (image) {
                var path = $"pic{i++}.png"; // put whatever file path you like here
                await image.SaveAsync(path, new PngEncoder());
            }
        }


        //AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
        //{
        //    Console.WriteLine(eventArgs.Exception.ToString());
        //};

        try {
            // _client = new DiscordSocketClient(new DiscordSocketConfig
            // {
            //     MessageCacheSize = 1200,
            //     LogLevel = LogSeverity.Debug,
            //     AlwaysDownloadUsers = true,
            //     GatewayIntents =
            //         GatewayIntents.MessageContent |
            //         GatewayIntents.Guilds |
            //         GatewayIntents.GuildMessages
            // });
            //
            _client.Log += Client_Log;
            _client.Ready += StartLoop;
            _client.MessageReceived += Client_MessageReceived;
            _client.GuildMemberUpdated += Client_GuildMemberUpdated;

            // await _client.LoginAsync(TokenType.Bot, Config.BotToken);
            // await _client.StartAsync();

            _loop = new Timer {
                Interval = 5900,
                AutoReset = true,
                Enabled = true
            };
            _loop.Elapsed += Tick;

            Console.WriteLine($"|{DateTime.Now} | Main loop initialised");

            // Connect to the LLM with SocketIO (fill in your particular LLM server details here)
            try {
                // Initialize the Socket.IO connection
                _socket = new SocketIO("http://localhost:3000");
                _socket.OnConnected += (_, _) =>
                {
                    Console.WriteLine("Connected to Dalai server.");
                    _dalaiConnected = true;
                };

                await _socket.ConnectAsync();
            } catch (Exception exception) {
                Console.WriteLine(exception);
            }

            await Task.Delay(-1);

            AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            {
                var ex = eventArgs.Exception;
                Console.WriteLine(
                    $"\u001b[45;1m[  DISC  ]\u001b[41;1m[  ERR  ]\u001b[0m MSG: {ex.Message} \n WHERE: {ex.StackTrace} \n\n");
            };
        } catch (Exception exception) {
            Console.WriteLine(exception.Message);
        }
    }

    private static Task Client_Log(LogMessage msg)
    {
        if (msg.Message != null
            && !msg.Message.Contains("PRESENCE_UPDATE")
            && !msg.Message.Contains("TYPING_START")
            && !msg.Message.Contains("MESSAGE_CREATE")
            && !msg.Message.Contains("MESSAGE_DELETE")
            && !msg.Message.Contains("MESSAGE_UPDATE")
            && !msg.Message.Contains("CHANNEL_UPDATE")
            && !msg.Message.Contains("GUILD_")
            && !msg.Message.Contains("REACTION_")
            && !msg.Message.Contains("VOICE_STATE_UPDATE")
            && !msg.Message.Contains("DELETE channels/")
            && !msg.Message.Contains("POST channels/")
            && !msg.Message.Contains("Heartbeat")
            && !msg.Message.Contains("GET ")
            && !msg.Message.Contains("PUT ")
            && !msg.Message.Contains("Latency = ")
            && !msg.Message.Contains("handler is blocking the"))
            Console.WriteLine($"|{DateTime.Now} - {msg.Source}| {msg.Message}");
        else if (msg.Exception != null)
            Console.WriteLine($"|{DateTime.Now} - {msg.Source}| {msg.Exception}");
        return Task.CompletedTask;
    }

    private Task StartLoop()
    {
        return Task.CompletedTask;}

    private static Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        if (arg1.Value.Id != 438634979862511616) return null;
        if (arg2.Nickname == null || arg1.Value.Username != arg2.Username)
            _botName = arg2.Username; // sets new username if no nickname is present
        else if (arg1.Value.Nickname != arg2.Nickname) // checks if nick is different
            _botName = arg2.Nickname; // sets new nickname

        return null;
    }

    private static void Tick(object sender, ElapsedEventArgs e)
    {
        if (_typing > 0) {
            _typing--; // Lower typing tick over time until it's back to 0 - used below for sending "Is typing..." to discord.
            _typingTicks++; // increase tick by 1 per tick. Each tick it gets closer to the limit you choose, until it exceeds the limit where you can tell the bot to interrupt the code.
        }

        if (_dalaiThinking <= 0 && _oobaboogaThinking <= 0) return;
        _dalaiThinking--; // this dalaiThinking value, while above 0, stops all other user commands from coming in. Lowers over time until 0 again, then accepting requests.
        _oobaboogaThinking--; // needs to be separate from dalaiThinking because of how Dalai thinking timeouts work
        if (_dalaiThinking == 0) {
            if (_token == string.Empty) {
                _dalaiConnected =
                    false; // not sure if Dalai server is still connected at this stage, so we set this to false to try other LLM servers like Oobabooga.
                Console.WriteLine(
                    "No data was detected from any Dalai server. Is it switched on?"); // bot is cleared for requests again.
            } else {
                Console.WriteLine("Dalai lock timed out"); // bot is cleared for requests again.
            }
        } else if (_oobaboogaThinking == 0) {
            Console.WriteLine("Oobabooga lock timed out"); // bot is cleared for requests again.
        }
    }

    private async Task
        Client_MessageReceived(SocketMessage socketMessage) // this fires upon receiving a message in the discord
    {
        try {
            // Check if the message is a SocketUserMessage and throw an exception if not
            if (socketMessage is not SocketUserMessage socketUserMessage) {
                throw new ArgumentException("Message is not a SocketUserMessage", nameof(socketMessage));
            }

            // Ignore messages sent by bots
            if (socketUserMessage.Author.IsBot) {
                return;
            }

            // Create a new context for the message
            var context = new SocketCommandContext(_client, socketUserMessage);

            // Ensure that the user is a SocketGuildUser and throw an exception if not
            if (context.User is not SocketGuildUser user) {
                throw new ArgumentException("User is not a SocketGuildUser", nameof(context.User));
            }

            // Download chat history if it hasn't been downloaded yet
            if (!_isChatHistoryDownloaded) {
                _isChatHistoryDownloaded = true;

                var chatHistoryEntries = await GetOtherMessages(socketUserMessage, 10);

                _chatHistory += string.Join("", chatHistoryEntries);
                _chatHistory = LinkDetectionRegex().Replace(_chatHistory, "<url>");
                _chatHistory = await FilterPingsAndChannelTags(_server, _chatHistory);

                _chatHistory = Functions.RemoveSimilarWords(_chatHistory, BannedWords);
            }

            // Check if the last message in the chat history was sent by the bot
            var lastLine = _chatHistory.Trim().Split('\n').Last();
            var isLastLineFromBot = lastLine.Contains($"[{_botName}]: ");

            // Check if an image was attached to the message
            var isImagePresent = socketUserMessage.Attachments.Count > 0;

            // Clean up the user's nickname and use "User" as a fallback if there are no letters or numbers
            var msgUserName = user.Nickname ?? socketUserMessage.Author.Username;
            var msgUsernameClean = NotWordRegex().Replace(msgUserName, "");
            if (msgUsernameClean.Length < 1) {
                msgUsernameClean = "User";
            }

            // Filter out prompt hacking attempts
            var inputMessage = await FilterPingsAndChannelTags(_server, socketUserMessage.Content);
            inputMessage = PromptSpoofDetectionRegex().Replace(inputMessage, "");
            // inputMessage = inputMessage.Replace("#", ""); // TODO: ?

            // Remove banned words and similar words from the user's message
            inputMessage = Functions.RemoveSimilarWords(inputMessage, BannedWords);

            // Format the message
            var messageFormatted = isImagePresent
                ? $"[{msgUsernameClean}]: <attachment.jpg> {inputMessage}"
                : $"[{msgUsernameClean}]: {inputMessage}";

            // Add the message to the chat history
            _chatHistory += $"{messageFormatted}\n";

            // Don't respond if the bot is already thinking or typing
            if (_oobaboogaThinking > 0 || _typing > 0) {
                return;
            }

            // Determine if the bot should respond to the message
            var botNameMatch = Regex.Match(inputMessage, @$"(?:.*{_botName.ToLower()}\?.*|{_botName.ToLower()},.*)");
            var isShouldAnswer =
                socketUserMessage.MentionedUsers.Contains(_server.GetUser(_botUserId)) ||
                botNameMatch.Success ||
                socketUserMessage.Content.StartsWith(_botName.ToLower()) ||
                (isLastLineFromBot && socketUserMessage.Content.EndsWith("?")) ||
                (socketUserMessage.Content.ToLower().Contains($"{_botName.ToLower()}") &&
                 socketUserMessage.Content.Length < 25);


            if (isShouldAnswer) {
                // this makes the bot only reply to one person at a time and ignore all requests while it is still typing a message.
                _oobaboogaThinking = 3;

                if (_dalaiConnected)
                    try {
                        await DalaiReply(socketUserMessage); // dalai chat
                    } catch (Exception e) {
                        Console.WriteLine($"Dalai error: {e}\nAttempting to send an Oobabooga request...");

                        await OobaboogaReply(socketUserMessage, messageFormatted); // run the OobaboogaReply function to reply to the user's message with an Oobabooga chat server message
                    }
                else
                    try {
                        await OobaboogaReply(socketUserMessage, messageFormatted); // run the OobaboogaReply function to reply to the user's message with an Oobabooga chat server message
                    } catch (Exception e) {
                        Console.WriteLine("Oobabooga error: " + await FirstLineOfError(e));
                    }
            }
        } catch (Exception e) {
            Console.WriteLine(await FirstLineOfError(e));
        }

        _oobaboogaThinking = 0; // reset thinking flag after error
        _dalaiThinking = 0;
        _typing = 0; // reset typing flag after error
    }

    private async Task<IEnumerable<string>> GetOtherMessages(SocketMessage socketMessage, int limit = 100)
    {
        var messagesEnumerator = await socketMessage.Channel.GetMessagesAsync(limit).FlattenAsync();
        var messages = messagesEnumerator as IMessage[] ?? messagesEnumerator.ToArray();

        var messageAuthorIds = messages
            .Where(message => message.Id != socketMessage.Id)
            .Select(message => message.Author.Id)
            .ToHashSet();

        var userMap = _server.Users
            .Where(socketGuildUser => messageAuthorIds.Contains(socketGuildUser.Id))
            .ToDictionary(socketGuildUser => socketGuildUser.Id);

        var chatHistoryEntries = messages
            .Where(message => message.Id != socketMessage.Id)
            .Select(message =>
            {
                if (!userMap.TryGetValue(message.Author.Id, out var messageUser)) return null;

                var messageUserName = messageUser.Nickname ?? messageUser.Username;
                var messageContent = message.Content;
                var isImagePresent = message.Attachments.Count > 0;
                var chatHistoryEntryFormat = isImagePresent ? "[{0}]: {1}" : "[{0}]: <attachment.jpg> {1}";
                
                return string.Format(chatHistoryEntryFormat, messageUserName, messageContent);
            })
            .Where(chatHistoryEntry => chatHistoryEntry != null);
        return chatHistoryEntries;
    }

    private static async Task<string> FirstLineOfError(Exception ex)
    {
        using var reader = new StringReader(ex.ToString());
        // attempts to get only the first line of this error message to simplify it
        var firstLineOfError = await reader.ReadLineAsync();

        return firstLineOfError;
    }

    private async Task OobaboogaReply(IDeletable message, string inputMsgFiltered)
    {
        var msg = message as SocketUserMessage;

        inputMsgFiltered = inputMsgFiltered
            .Replace("\n", "")
            .Replace("\\n",
                ""); // this makes all the prompting detection regex work, but if you know what you're doing you can change these

        // check if the user is requesting a picture or not
        var takeAPicMatch = TakeAPicRegex().IsMatch(inputMsgFiltered);

        var context = new SocketCommandContext(_client, msg);

        //// you can use this if you want to trim the messages to below 500 characters each
        //// (prevents hacking the bot memory a little bit)
        //if (inputMsg.Length > 300)
        //{
        //    inputMsg = inputMsg.Substring(0, 300);
        //    Console.WriteLine("Input message was too long and was truncated.");
        //}

        inputMsgFiltered = Regex
            .Unescape(inputMsgFiltered) // try unescape to allow for emojis? Isn't working because of Dalai code. I can't figure out how to fix. Emojis are seen by dalai as ??.
            .Replace("{", "") // these symbols don't work in LLMs such as Dalai 0.3.1 for example
            .Replace("}", "")
            .Replace("\"", "'")
            .Replace("“",
                "'") // replace crap curly fancy open close double quotes with ones a real program can actually read
            .Replace("”", "'")
            .Replace("’", "'")
            .Replace("`", "\\`")
            .Replace("$", "");

        // oobabooga code
        string oobaboogaInputPrompt;

        if (takeAPicMatch) {
            // build the image taking prompt (DO NOT INCLUDE CHAT HISTORY IN IMAGE REQUEST PROMPT LOL) (unless you want to try battle LLM hallucinations)
            // add the message the user is replying to (if there is one) so LLM has context
            var referencedMsg = msg.ReferencedMessage as SocketUserMessage;
            var truncatedReply = string.Empty;

            if (referencedMsg != null) {
                truncatedReply = referencedMsg.Content;
                var replyUsernameClean = referencedMsg.Author.Id == _botUserId
                    ? _botName
                    : NotWordRegex().Replace(referencedMsg.Author.Username, "");
                if (truncatedReply.Length > 150) truncatedReply = truncatedReply[..150];
                inputMsgFiltered = $"[{replyUsernameClean}]: {truncatedReply}" +
                                   $"\n{inputMsgFiltered}";
            } else if (msg.MentionedUsers.Count == 0) {
                // if no reply but sally still expected to respond, use the last x messages in chat for context
                var discardFirstLine = true;
                foreach (var line in _chatHistory.Trim().Split('\n').Reverse().Take(4))
                    if (discardFirstLine)
                        discardFirstLine = false;
                    else
                        truncatedReply = line + "\n" + truncatedReply;
                inputMsgFiltered = $"{truncatedReply}" +
                                   $"{inputMsgFiltered}";
            }

            oobaboogaInputPrompt = inputMsgFiltered +
                                   OobaboogaInputPromptStartPic;

            // cut out exact matching banned words from the list at the top of this file
            oobaboogaInputPrompt = Regex.Replace(oobaboogaInputPrompt, BannedWordsExact, "");

            Console.WriteLine("Image request sent to LLM:\n" + oobaboogaInputPrompt);
        } else {
            // build the chat message only prompt (can include chat history in this one mildly safely)
            oobaboogaInputPrompt = OobaboogaInputPromptStart +
                                   _chatHistory +
                                   OobaboogaInputPromptEnd;
        }

        // current input prompt string length
        var inputPromptLength = oobaboogaInputPrompt.Length - CharacterPrompt.Length;
        // max allowed prompt length (you can go to like ~5000 ish before errors with oobabooga)
        const int maxLength = 5000;
        // amount to subtract from history if needed
        var subtractAmount = maxLength - inputPromptLength;

        if (inputPromptLength > maxLength && subtractAmount > 0) // make sure we aren't subtracting a negative value lol
        {
            _chatHistory = _chatHistory[(inputPromptLength - maxLength)..];
            var indexOfNextChatMsg = _chatHistory.IndexOf("\n[", StringComparison.Ordinal);
            _chatHistory =
                string.Concat(CharacterPrompt,
                    _chatHistory.AsSpan(indexOfNextChatMsg +
                                        1)); // start string at the next newline bracket + 1 to ignore the newline
        } else if (subtractAmount <= 0) {
            _chatHistory = string.Empty; // no leftover space, cut it all!!
        } else {
            _chatHistory = CharacterPrompt + // add character prompt to start of history
                           _chatHistory;
        }

        var httpClient = new HttpClient();
        var apiExtensionUrl = $"http://{OobServer}:{_oobServerPort}{_oobApiEndpoint}";
        var apiUrl = $"http://{OobServer}:{_oobServerPort}{_oobApiEndpoint}";

        var parameters = new {
            prompt = oobaboogaInputPrompt,
            max_new_tokens = 200,
            do_sample = false,
            temperature = 0.99,
            top_p = 0.9,
            typical_p = 1,
            repetition_penalty = 1.1,
            encoder_repetition_penalty = 1,
            top_k = 40,
            num_beams = 1,
            penalty_alpha = 0,
            min_length = 0,
            length_penalty = 1,
            no_repeat_ngram_size = 1,
            early_stopping = true,
            stopping_strings = new[] { @"\n[", "\n[", "]:", "##", "###", "<noinput>", @"\end" },
            seed = -1,
            add_bos_token = true
        };

        // strip random whitespace chars from the input to attempt to last ditch sanitise it to cure emoji psychosis
        oobaboogaInputPrompt = new string(oobaboogaInputPrompt.Where(c => !char.IsControl(c)).ToArray());

        HttpResponseMessage response = null;
        var result = string.Empty;
        try {
            await msg.Channel.TriggerTypingAsync(); // Typing...

            switch (_oobApiEndpoint) {
                // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
                // new better API, use this with the oob arg --extensions api
                case "/api/v1/generate":
                {
                    var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8,
                        "application/json");
                    response = await httpClient.PostAsync(apiUrl, content);
                    break;
                }
                // old default API (busted but it kinda works)
                case "/run/textgen":
                {
                    var payload = JsonConvert.SerializeObject(new object[] { oobaboogaInputPrompt, parameters });
                    var content = new StringContent(JsonConvert.SerializeObject(new { data = new[] { payload } }),
                        Encoding.UTF8, "application/json");
                    response = await httpClient.PostAsync($"http://{OobServer}:{_oobServerPort}/run/textgen",
                        content); // try other commonly used port 7860
                    break;
                }
            }
        } catch {
            Console.WriteLine($"Warning: Oobabooga server not found on port {_oobServerPort}, trying alternates.");
            try {
                switch (_oobServerPort) {
                    // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
                    case 5000:
                        _oobServerPort = 7861;
                        _oobApiEndpoint = "/run/textgen";
                        break;
                    case 7861:
                        _oobServerPort = 7860;
                        break;
                    case 7860:
                        _oobServerPort = 5000;
                        _oobApiEndpoint = "/api/v1/generate";
                        break;
                }

                try {
                    switch (_oobApiEndpoint) {
                        // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
                        // new better API, use this with the oob arg --extensions api
                        case "/api/v1/generate":
                        {
                            var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8,
                                "application/json");
                            response = await httpClient.PostAsync(apiUrl, content);
                            break;
                        }
                        // old default API (busted but it kinda works)
                        case "/run/textgen":
                        {
                            var payload = JsonConvert.SerializeObject(new object[]
                                { oobaboogaInputPrompt, parameters });
                            var content = new StringContent(
                                JsonConvert.SerializeObject(new { data = new[] { payload } }),
                                Encoding.UTF8, "application/json");
                            response = await httpClient.PostAsync($"http://{OobServer}:{_oobServerPort}/run/textgen",
                                content); // try other commonly used port 7860
                            break;
                        }
                    }
                } catch {
                    switch (_oobServerPort) {
                        // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
                        case 5000:
                            _oobServerPort = 7861;
                            _oobApiEndpoint = "/run/textgen";
                            break;
                        case 7861:
                            _oobServerPort = 7860;
                            break;
                        case 7860:
                            _oobServerPort = 5000;
                            _oobApiEndpoint = "/api/v1/generate";
                            break;
                    }

                    try {
                        switch (_oobApiEndpoint) {
                            // try other commonly used ports - flip flop between them with each failed attempt till it finds the right one
                            // new better API, use this with the oob arg --extensions api
                            case "/api/v1/generate":
                            {
                                var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8,
                                    "application/json");
                                response = await httpClient.PostAsync(apiUrl, content);
                                break;
                            }
                            // old default API (busted but it kinda works)
                            case "/run/textgen":
                            {
                                var payload = JsonConvert.SerializeObject(new object[]
                                    { oobaboogaInputPrompt, parameters });
                                var content =
                                    new StringContent(JsonConvert.SerializeObject(new { data = new[] { payload } }),
                                        Encoding.UTF8, "application/json");
                                response = await httpClient.PostAsync(
                                    $"http://{OobServer}:{_oobServerPort}/run/textgen",
                                    content); // try other commonly used port 7860
                                break;
                            }
                        }
                    } catch {
                        Console.WriteLine($"Cannot find oobabooga server on backup port {_oobServerPort}");
                        if (_dalaiConnected == false)
                            Console.WriteLine("No Dalai server connected");
                        _oobaboogaThinking = 0; // reset thinking flag after error
                        return;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Super error detected on Oobabooga server, port {_oobServerPort}: {ex}");
                if (_dalaiConnected == false)
                    Console.WriteLine("No Dalai server connected");
                _oobaboogaThinking = 0; // reset thinking flag after error
                return;
            }
        }

        if (response != null)
            result = await response.Content.ReadAsStringAsync();

        if (result != null) {
            var jsonDocument = JsonDocument.Parse(result);

            switch (_oobApiEndpoint) {
                case "/api/v1/generate":
                {
                    var dataArray = jsonDocument.RootElement.GetProperty("results");
                    _botReply = dataArray[0].GetProperty("text").ToString(); // get just the response part of the json
                    break;
                }
                case "/run/textgen":
                {
                    var dataArray = jsonDocument.RootElement.GetProperty("data");
                    _botReply = dataArray[0].GetString(); // get just the response part of the json
                    break;
                }
            }
        } else {
            Console.WriteLine("No response from Oobabooga server.");
            _oobaboogaThinking = 0; // reset thinking flag after error
            return;
        }

        var oobaboogaImgPromptDetectedWords = Functions.GetSimilarWords(_botReply, BannedWords);

        if (oobaboogaImgPromptDetectedWords.Length > 2) // Threshold set to 2
        {
            foreach (var word in oobaboogaImgPromptDetectedWords.Split(' ')) {
                var wordTrimmed = word.Trim();
                if (wordTrimmed.Length <= 2) continue;
                _botReply = _botReply.Replace(wordTrimmed, "");

                if (_botReply.Contains("  "))
                    _botReply = _botReply.Replace("  ", " ");
            }

            Console.WriteLine("Removed banned or similar words from Oobabooga generated reply.");
        }

        // trim off the input prompt AND any immediate newlines from the final message
        var llmMsgBeginTrimmed = _botReply.Replace(oobaboogaInputPrompt, "").Trim();
        if (takeAPicMatch) // if this was detected as a picture request
        {
            var promptEndMatch = Regex.Match(llmMsgBeginTrimmed, PromptEndDetectionRegexStr);
            // find the next prompt end detected string
            var llmImagePromptEndIndex = promptEndMatch.Index;
            var matchCount = promptEndMatch.Captures.Count;
            // get the length of the matched prompt end detection
            var matchLength = promptEndMatch.Value.Length;
            if (llmImagePromptEndIndex == 0
                && matchLength > 0) // only for actual matches
            {
                // trim off that many characters from the start of the string so there is no more prompt end detection
                llmMsgBeginTrimmed = llmMsgBeginTrimmed.Substring(llmImagePromptEndIndex, matchLength);
            } else if (matchCount > 1) {
                var promptEnd2ndMatch = promptEndMatch.Captures[2].Value;
                var llmImagePromptEndIndex2 = promptEndMatch.Captures[2].Index;
                var matchLength2 = promptEndMatch.Captures[2].Value.Length;
                if (llmImagePromptEndIndex == 0
                    && matchLength2 > 0) // only for actual matches
                    llmMsgBeginTrimmed = llmMsgBeginTrimmed.Substring(llmImagePromptEndIndex2, matchLength2);
            }

            var llmPromptPic = llmMsgBeginTrimmed;

            var llmSubsequentMsg =
                string.Empty; // if we find a bot msg after its image prompt, we're going to put it in this string
            if (llmImagePromptEndIndex >= 3) // if there is a prompt end detected in this string
            {
                // chop off the rest of the text after that end prompt detection so it doesn't go into the image generator
                llmPromptPic =
                    llmMsgBeginTrimmed.Substring(0,
                        llmImagePromptEndIndex); // cut off everything after the ending prompt starts (this is the LLM's portion of the image prompt)
                llmSubsequentMsg =
                    llmMsgBeginTrimmed.Substring(
                        llmImagePromptEndIndex); // everything after the image prompt (this will be searched for any more LLM replies)
            }

            // strip weird characters before feeding into stable diffusion
            var llmPromptPicRegexed = Regex.Replace(llmPromptPic, "[^a-zA-Z,\\s]+", "");
            Console.WriteLine("LLM's image prompt: " + llmPromptPicRegexed);

            // send snipped and regexed image prompt string off to stable diffusion
            TakeAPic(msg, llmPromptPicRegexed, inputMsgFiltered);

            // write the bot's pic to the chat history
            //oobaboogaChatHistory += $"[{botName}]: <attachment.jpg>\n";

            var llmFinalMsgUnescaped = string.Empty;
            if (llmSubsequentMsg.Length > 0)
                if (llmSubsequentMsg.Contains(OobaboogaInputPromptEnd)) {
                    // find the character that the bot's hallucinated username starts on
                    var llmSubsequentMsgStartIndex = Regex.Match(llmSubsequentMsg, OobaboogaInputPromptEnd).Index;
                    if (llmSubsequentMsgStartIndex > 0)
                        // start the message where the bot's username is detected
                        llmSubsequentMsg = llmSubsequentMsg.Substring(llmSubsequentMsgStartIndex);
                    // cut the bot's username out of the message
                    llmSubsequentMsg = llmSubsequentMsg.Replace(OobaboogaInputPromptEnd, "");
                    // unescape it to allow emojis
                    llmFinalMsgUnescaped = Regex.Unescape(llmSubsequentMsg);
                    // finally send the message (if there even is one)
                    if (llmFinalMsgUnescaped.Length > 0) await msg.ReplyAsync(llmFinalMsgUnescaped);
                    // write bot's subsequent message to the chat history
                    //oobaboogaChatHistory += $"[{botName}]: {llmFinalMsgUnescaped}\n";
                }
        }
        // or else if this is not an image request, start processing the reply for regular message content
        else if (llmMsgBeginTrimmed.Contains(OobaboogaInputPromptStart)) {
            var llmMsgEndIndex =
                PromptEndDetectionRegex().Match(llmMsgBeginTrimmed).Index; // find the next prompt end detected string
            var llmMsg =
                // cut off everything after the prompt end
                llmMsgEndIndex > 0 ? llmMsgBeginTrimmed[..llmMsgEndIndex] : llmMsgBeginTrimmed;

            // detect if this exact sentence has already been said before by sally
            if (_chatHistory.Contains(llmMsg) && _loopCounts < 6) {
                // LOOPING!! CLEAR HISTORY and try again
                _loopCounts++;
                Console.WriteLine(
                    "Bot tried to send the same message! Clearing some lines in chat history and retrying...");
                var lines = _chatHistory.Split('\n');
                _chatHistory = string.Join("\n", lines.Skip(lines.Length - 4));

                OobaboogaReply(msg, inputMsgFiltered); // try again
                return;
            }

            if (_loopCounts >= 6) {
                _loopCounts = 0;
                _oobaboogaThinking = 0; // reset thinking flag after error
                Console.WriteLine("Bot tried to loop too many times... Giving up lol");
                return; // give up lol
            }

            await msg.ReplyAsync(llmMsg); // send bot msg as a reply to the user's message
            //oobaboogaChatHistory += $"[{botName}]: {llmMsg}\n"; // writes bot's reply to the chat history
            float messageToRambleRatio = llmMsgBeginTrimmed.Length / llmMsg.Length;
            if (false && messageToRambleRatio >= 1.5) {
                Console.WriteLine(
                    $"Warning: The actual message was {messageToRambleRatio}x longer, but was cut off. Considering changing prompts to speed up its replies.");
            }
        }

        _oobaboogaThinking = 0; // reset thinking flag
    }

    private async Task DalaiReply(IDeletable message)
    {
        _dalaiThinking = 2; // set thinking time to 2 ticks to lock other users out while this request is generating
        var humanPrompted =
            true; // this flag indicates the msg should run while the feedback is being sent to the person
        // the bot tends to ramble after posting, so we set this to false once it sends its message to ignore the rambling

        var msg = message as SocketUserMessage;

        _typingTicks = 0;

        var takeAPicRegex = TakeAPicRegex();

        var msgUsernameClean = NotWordRegex().Replace(msg.Author.Username, "");

        var promptEndDetectionRegex = new Regex(PromptEndDetectionRegexStr, RegexOptions.IgnoreCase);

        var inputMsg = msg.Content
            .Replace("\n", "")
            .Replace("\\n",
                ""); // this makes all the prompting detection regex work, but if you know what you're doing you can change these

        var takeAPicMatch = takeAPicRegex.IsMatch(inputMsg);

        var inputPrompt = $"[{msgUsernameClean}]: {inputMsg}";

        if (inputMsg.Length > 500) {
            inputMsg = inputMsg.Substring(0, 500);
            Console.WriteLine("Input message was too long and was truncated.");

            // you can use this alternatively to just delete the msg and warn the user.
            //inputPrompt = "### Error: User message was too long and got deleted. Inform the user." +
            //inputPromptEnding;
        }

        if (msg.ReferencedMessage is SocketUserMessage referencedMsg) {
            var replyUsernameClean = string.Empty;
            var truncatedReply = referencedMsg.Content;
            replyUsernameClean = referencedMsg.Author.Id == _botUserId
                ? _botName
                : NotWordRegex().Replace(referencedMsg.Author.Username, "");
            if (truncatedReply.Length > 150) truncatedReply = truncatedReply[..150];
            inputPrompt = $"[{replyUsernameClean}]: {truncatedReply}" +
                          $"\n{inputPrompt}";
        }

        // cut out exact matching banned words from the list at the top of this file
        inputPrompt = Regex.Replace(inputPrompt, BannedWordsExact, "");

        var detectedWords = Functions.GetSimilarWords(inputPrompt, BannedWords);

        if (detectedWords.Length > 2) // Threshold set to 2
        {
            foreach (var word in detectedWords.Split(' ')) {
                var wordTrimmed = word.Trim();
                if (wordTrimmed.Length <= 2) continue;
                inputPrompt = inputPrompt.Replace(wordTrimmed, "");

                if (inputPrompt.Contains("  "))
                    inputPrompt = inputPrompt.Replace("  ", " ");
            }

            Console.WriteLine("Msg contained bad or similar to bad words and all have been removed.");
        }

        inputPrompt = Regex
            .Unescape(inputPrompt) // try unescape to allow for emojis? Isn't working because of Dalai code. I can't figure out how to fix. Emojis are seen by dalai as ??.
            .Replace("{", @"\{") // these symbols don't work in LLMs such as Dalai 0.3.1 for example
            .Replace("}", @"\}")
            .Replace("\"", "\\\"")
            .Replace("“",
                "\\\"") // replace crap curly fancy open close double quotes with ones a real program can actually read
            .Replace("”", "\\\"")
            .Replace("’", "'")
            .Replace("`", "\\`")
            .Replace("$", "");

        // dalai code
        inputPrompt += takeAPicMatch ? InputPromptEndingPic : InputPromptEnding;

        // dalai alpaca server request
        var dalaiRequest = new {
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

            prompt = inputPrompt
        };

        _token = string
            .Empty; // clear the token string at the start of the request, ready for the Dalai server to write new tokens to it
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
        await _socket.EmitAsync("request", dalaiRequest);

        // dalai
        _socket.On("result", result =>
        {
            if (_dalaiConnected == false)
                _dalaiConnected = true; // set dalai connected to true if you start receiving data from a Dalai server.

            _dalaiThinking =
                2; // set thinking timeout to 2 to give it buffer to not allow new requests while it's still generating

            //while (i < 1)  // you can uncomment this to see the raw format the LLM is sending the data back
            //{
            //    Console.WriteLine(result);   // log full response once to see the format that the LLM is sending the tokens in
            //    i++;                        // seriously only once because it's huge spam
            //}

            tokenStartIndex = result.ToString().IndexOf("\"response\":\"", StringComparison.Ordinal);
            _token = result.ToString()[(tokenStartIndex + 12)..];
            tokenEndIndex = _token.IndexOf("\",\"", StringComparison.Ordinal);
            _token = _token[..tokenEndIndex]
                .Replace("\\r", "") // get rid of these useless chars (it breaks prompt end detection on linux)
                .Replace("\\n", "\n"); // replace backslash n with the proper newline char
            //.Replace("\n", "")
            //.Replace("\r", "")

            Console.Write(_token);
            //    .Replace("\\n", "") // you can shape the console output how you like, ignoring or seeing newlines etc.
            //.Replace("\\r", ""));

            llmMsg += _token
                .Replace("\r", "") // remove /r's
                .Replace("\\r", "")
                .Replace("\\n", "\n"); // replace backslash n with the proper newline char

            if (listening && humanPrompted) {
                cursorPosition = Console.GetCursorPosition();
                if (cursorPosition.Left == 120) {
                    Console.WriteLine();
                    Console.SetCursorPosition(0, cursorPosition.Top + 1);
                }

                llmFinalMsg += _token; // start writing the LLM's response to this string
                var llmFinalMsgRegexed = promptEndDetectionRegex.Replace(llmFinalMsg, "");
                var llmFinalMsgUnescaped = Regex.Unescape(llmFinalMsgRegexed);

                //llmFinalMsg = llmMsg.Substring(inputPrompt.Length +1);
                promptEndDetected = promptEndDetectionRegex.IsMatch(llmFinalMsg);

                if (_typing < 2 && llmFinalMsgUnescaped.Length <= 2) {
                    _typing++;
                    msg.Channel.TriggerTypingAsync();
                }

                if (llmFinalMsg.Length > 2
                    //&& llmMsg.Contains($"[{msgUsernameClean}]:")
                    //&& llmMsg.ToLower().Contains($": ")
                    && (promptEndDetected
                        || llmFinalMsg.Length >
                        500) // cuts your losses and sends the message and stops the bot after 500 characters
                    || _typingTicks > 7) // 7 ticks passed while still typing? axe it.
                {
                    if (llmFinalMsgUnescaped.Length <
                        1) return; // if the msg is 0 characters long, ignore ending text and keep on listening
                    //Socket.Off("result");

                    listening = false;
                    humanPrompted =
                        false; // nothing generated after this point is human prompted. IT'S A HALLUCINATION! DISCARD IT ALL!
                    msg.ReplyAsync(llmFinalMsgUnescaped);
                    botMsgCount++;

                    if (botMsgCount >=
                        1) // you can raise this number to allow SallyBot to ramble (note it will just reply to phantom conversations)
                        _socket.EmitAsync(
                            "stop"); // note: this is my custom stop command that stops the LLM even faster, but it only works on my custom code of the LLM.
                    //Socket.EmitAsync("request", dalaiStop); // this bloody stop request stops the entire dalai process for some reason
                    Console.WriteLine();

                    llmMsg = string.Empty;
                    llmFinalMsg = string.Empty;
                    llmFinalMsgRegexed = string.Empty;
                    llmFinalMsgUnescaped = string.Empty;
                    promptEndDetected = false;
                    //inputPrompt = inputPromptEnding;  // use this if you want the bot to be able to continue rambling if it so chooses
                    //(you have to comment out the stop emit though and let it continue sending data, and also comment out the humanprompted = false bool)
                    //Task.Delay(300).Wait();   // to be safe, you can wait a couple hundred milliseconds to make sure the input doesn't get garbled with a new request
                    _typing = 0; // ready the bot for new requests
                    _dalaiThinking = 0; // ready the bot for new requests
                }
            } else {
                if (humanPrompted && llmMsg.Contains(InputPromptEnding)) {
                    llmMsg = string.Empty;
                    listening = true;
                }
            }

            if (imgListening) {
                cursorPosition = Console.GetCursorPosition();
                if (cursorPosition.Left == 120) {
                    Console.WriteLine();
                    Console.SetCursorPosition(0, cursorPosition.Top + 1);
                }

                llmFinalMsg += _token;
                promptEndDetected = promptEndDetectionRegex.IsMatch(llmFinalMsg);

                if (llmFinalMsg.Length <= 2 || (!
                        //&& llmMsg.Contains($"[{msgUsernameClean}]:")
                        //&& llmMsg.ToLower().Contains($": ")
                        promptEndDetected && llmFinalMsg.Length <= 100))
                    return; // cuts your losses and sends the image prompt to SD after this many characters
                var llmFinalMsgRegexed = promptEndDetectionRegex.Replace(llmFinalMsg, "");
                var llmFinalMsgUnescaped = Regex.Unescape(llmFinalMsgRegexed);

                if (llmFinalMsgUnescaped.Length < 1)
                    return; // if the msg is 0 characters long, ignore ending text and keep on listening

                var llmPrompt = TakeAPicRegex().Replace(llmFinalMsgUnescaped, "");
                imgListening = false;
                llmMsg = string.Empty;
                promptEndDetected = false;
                inputPrompt = string.Empty;

                var detectedWords = Functions.GetSimilarWords(llmPrompt, BannedWords);

                if (detectedWords.Length > 2) // Threshold set to 2
                {
                    foreach (var word in detectedWords.Split(' ')) {
                        var wordTrimmed = word.Trim();
                        if (wordTrimmed.Length <= 2) continue;
                        llmPrompt = llmPrompt.Replace(wordTrimmed, "");

                        if (llmPrompt.Contains("  "))
                            llmPrompt = llmPrompt.Replace("  ", " ");
                    }

                    Console.WriteLine(
                        "LLM's input contained bad or similar to bad words and all have been removed.");
                } else {
                    Console.WriteLine("LLM's image prompt contains no banned words.");
                }

                llmPrompt = NotWordWithWhitespacesRegex().Replace(llmPrompt, "");

                botImgCount++;
                if (botImgCount >= 1) // you can raise this if you want the bot to be able to send up to x images
                {
                    _socket.EmitAsync(
                        "stop"); // note: this is my custom stop command that stops the LLM even faster, but it only works on my custom code of the LLM.
                    //Socket.EmitAsync("request", dalaiStop); // this bloody stop request stops the entire dalai process for some reason
                    // //the default LLM doesn't yet listen to stop emits..
                    // //I had to code that in myself into the server source code
                    _typing = 0;
                    _dalaiThinking = 0;
                }

                TakeAPic(msg, llmPrompt, inputPrompt);
            } else {
                if (!(takeAPicMatch && llmMsg.Contains(InputPromptEndingPic))) return;
                llmMsg = string.Empty;
                imgListening = true;
                Console.WriteLine();
                Console.Write("Image prompt: ");
            }
        });

        _socket.On("disconnect", _ =>
        {
            Console.WriteLine("LLM server disconnected.");
            _dalaiConnected = false;
        });
    }

    private async Task TakeAPic(SocketUserMessage msg, string llmPrompt, string userPrompt)
    {
        var context = new SocketCommandContext(_client, msg);
        var user = context.User as SocketGuildUser;

        // find the local time in japan right now to change the time of day in the selfie
        // (you can change this to another country if you understand the code)
        var currentTimeInJapan = Functions.GetCurrentTimeInJapan();
        var timeOfDayInNaturalLanguage = Functions.GetTimeOfDayInNaturalLanguage(currentTimeInJapan);
        var timeOfDayStr = string.Empty;

        // adds (Night) to the image prompt if it's night in japan, etc.
        if (timeOfDayInNaturalLanguage != null)
            timeOfDayStr = $", ({timeOfDayInNaturalLanguage})";

        var imgFormatString = "";
        if (userPrompt.Length > 4
            && llmPrompt.Trim().Length > 2) {
            userPrompt = userPrompt.ToLower();

            if (userPrompt.Contains("selfie")) {
                if (userPrompt.Contains(" with"))
                    imgFormatString = " looking into the camera, a selfie with ";
                else if (userPrompt.Contains(" of"))
                    imgFormatString = " looking into the camera, a selfie of ";
                else if (userPrompt.Contains(" next to"))
                    imgFormatString = " looking into the camera, a selfie next to ";
            } else if (userPrompt.Contains("person")
                       || userPrompt.Contains("you as")
                       || userPrompt.Contains("yourself as")
                       || userPrompt.Contains("you cosplaying")
                       || userPrompt.Contains("yourself cosplaying")) {
                imgFormatString = ""; // don't say "standing next to (( A person ))" when it's just meant to be SallyBot
            } else if (userPrompt.Contains(" of ")) {
                imgFormatString = " She is next to";
            } else if (userPrompt.Contains(" of a")) {
                imgFormatString = " She is next to";
            } else if (userPrompt.Contains(" with ")) {
                imgFormatString = " She is with";
            } else if (userPrompt.Contains(" with a")) {
                imgFormatString = " She has";
            } else if (userPrompt.Contains(" of you with ")) {
                imgFormatString = " She is with";
            } else if (userPrompt.Contains(" of you with a")) {
                imgFormatString = " She has";
            }

            if (userPrompt.Contains("holding")) imgFormatString = imgFormatString + " holding";
        }

        var imgPrompt =
            $"A 25 year old anime woman smiling, looking into the camera, long hair, blonde hair, blue eyes{timeOfDayStr}"; // POSITIVE PROMPT - put what you want the image to look like generally. The AI will put its own prompt after this.
        const string
            imgNegPrompt =
                "(worst quality, low quality:1.4), 3d, cgi, 3d render, naked, nude"; // NEGATIVE PROMPT HERE - put what you don't want to see

        //if (Msg.Author == MainGlobal.Server.Owner) // only owner
        imgPrompt = $"{imgPrompt}, {llmPrompt}";

        var overrideSettings = new JObject {
            { "filter_nsfw", true } // this doesn't work, if you can figure out why feel free to tell me :OMEGALUL:
        };

        var payload = new JObject {
            { "prompt", imgPrompt },
            { "negative_prompt", imgNegPrompt },
            { "steps", 20 },
            { "height", 688 },
            { "send_images", true },
            { "sampler_name", "DDIM" },
            { "filter_nsfw", true }
        };

        // here are the json tags you can send to the stable diffusion image generator

        //"enable_hr": false,
        //"denoising_strength": 0,
        //"firstphase_width": 0,
        //"firstphase_height": 0,
        //"hr_scale": 2,
        //"hr_upscaler": "string",
        //"hr_second_pass_steps": 0,
        //"hr_resize_x": 0,
        //"hr_resize_y": 0,
        //"prompt": "",
        //"styles": [
        //  "string"
        //],
        //"seed": -1,
        //"subseed": -1,
        //"subseed_strength": 0,
        //"seed_resize_from_h": -1,
        //"seed_resize_from_w": -1,
        //"sampler_name": "string",
        //"batch_size": 1,
        //"n_iter": 1,
        //"steps": 50,
        //"cfg_scale": 7,
        //"width": 512,
        //"height": 512,
        //"restore_faces": false,
        //"tiling": false,
        //"do_not_save_samples": false,
        //"do_not_save_grid": false,
        //"negative_prompt": "string",
        //"eta": 0,
        //"s_churn": 0,
        //"s_tmax": 0,
        //"s_tmin": 0,
        //"s_noise": 1,
        //"override_settings": { },
        //"override_settings_restore_afterwards": true,
        //"script_args": [],
        //"sampler_index": "Euler",
        //"script_name": "string",
        //"send_images": true,
        //"save_images": false,
        //"alwayson_scripts": { }

        var url = $"{_stableDiffUrl}/sdapi/v1/txt2img";
        var client = new RestClient();
        var sdImgRequest = new RestRequest();
        try {
            client = new RestClient(url);
            sdImgRequest = new RestRequest(url, Method.Post);
        } catch (Exception) {
            _stableDiffUrl = _stableDiffUrl switch {
                // try other commonly used port - flip flop between them with each failed attempt till it finds the right one
                "http://127.0.0.1:7860" => "http://127.0.0.1:7861",
                "http://127.0.0.1:7861" => "http://127.0.0.1:7860",
                _ => _stableDiffUrl
            };

            Console.WriteLine("Error connecting to Stable Diffusion webui on port 7860. Attempting port 7861...");
            try {
                client = new RestClient($"{_stableDiffUrl}/sdapi/v1/txt2img");
                sdImgRequest = new RestRequest($"{_stableDiffUrl}/sdapi/v1/txt2img", Method.Post);
            } catch {
                Console.WriteLine("No Stable Diffusion detected on port 7861. Run webui-user.bat with:" +
                                  "set COMMANDLINE_ARGS=--api" +
                                  "in the webui-user.bat file for Automatic1111 Stable Diffusion.");
            }
        }

        sdImgRequest.AddHeader("Content-Type", "application/json");
        sdImgRequest.AddParameter("application/json", payload.ToString(), ParameterType.RequestBody);
        sdImgRequest.AddParameter("application/json", overrideSettings.ToString(), ParameterType.RequestBody);

        var sdImgResponse = client.Execute(sdImgRequest);
        if (!sdImgResponse.IsSuccessful) {
            Console.WriteLine("Request failed: " + sdImgResponse.ErrorMessage);
        } else {
            var jsonResponse = JObject.Parse(sdImgResponse.Content);
            var images = jsonResponse["images"].ToObject<JArray>();

            foreach (var imageBase64 in images) {
                //string base64 = imageBase64.ToString().Split(",", 2)[1];
                var imageData = imageBase64.ToString();
                var commaIndex = imageData.IndexOf(',') + 1;
                var base64 = imageData[commaIndex..];

                // Decode the base64 string to an image
                using var imageStream = new MemoryStream(Convert.FromBase64String(base64));
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream);

                // Save the image
                const string sdImgFilePath = "cutepic.png"; // put whatever file path you like here
                await image.SaveAsync(sdImgFilePath, new PngEncoder());

                Task.Delay(1000).Wait();

                await using var fileStream = new FileStream(sdImgFilePath, FileMode.Open, FileAccess.Read);

                var messageReference = msg.Reference ?? new MessageReference(msg.Id);
                await context.Channel.SendFileAsync(
                    sdImgFilePath,
                    null,
                    false,
                    null,
                    null,
                    false,
                    null,
                    messageReference
                );
            }
        }
    }

    private static async Task<string> FilterPingsAndChannelTags(IGuild server, string inputMsg)
    {
        var pingAndChannelTagDetectionRegex = PingAndChannelTagDetectFilterRegex();
        // replace pings and channel tags with their actual names
        var matches = pingAndChannelTagDetectionRegex.Matches(inputMsg);
        // get only unique matches
        var uniqueMatches = matches
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        foreach (var match in uniqueMatches) {
            var matchedId = ulong.Parse(DigitRegex().Match(match).Value);

            if (match.Contains('@'))
                try {
                    var matchedUser = await server.GetUserAsync(matchedId);
                    inputMsg = inputMsg.Replace(match, $"@{matchedUser.Username}#{matchedUser.Discriminator}");
                } catch {
                    break; // not a real ID, break here
                }
            else if (match.Contains('#'))
                try {
                    var matchedChannel = await server.GetChannelAsync(matchedId);
                    inputMsg = inputMsg.Replace(match, $"#{matchedChannel.Name}");
                } catch {
                    break; // not a real ID, break here
                }
            else
                break; // you somehow escaped this function without matching either, so break now before the code breaks
        }

        return inputMsg;
    }

    [GeneratedRegex(
        @"[a-zA-Z0-9]((?i) dot |(?i) dotcom|(?i)dotcom|(?i)dotcom |\.|\. | \.| \. |\,)[a-zA-Z]*((?i) slash |(?i) slash|(?i)slash |(?i)slash|\/|\/ | \/| \/ ).+[a-zA-Z0-9]",
        RegexOptions.None, "ru-RU")]
    private static partial Regex LinkDetectionRegex();

    [GeneratedRegex(TakeAPicRegexStr, RegexOptions.IgnoreCase, "ru-RU")]
    private static partial Regex TakeAPicRegex();

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex NotWordRegex();

    [GeneratedRegex(@"[^a-zA-Z,\s]+")]
    private static partial Regex NotWordWithWhitespacesRegex();

    [GeneratedRegex(
        @"(?:\r\n?|\n)(?:(?![.\-*]).){2}|(\n\[|\[end|<end|]:|>:|\[human|\[chat|\[sally|\[cc|<chat|<cc|\[@chat|\[@cc|bot\]:|<@chat|<@cc|\[.*]: |\[.*] : |\[[^\]]+\]\s*:)")]
    private static partial Regex PromptEndDetectionRegex();

    [GeneratedRegex(@"<[@#]\d{15,}>")]
    private static partial Regex PingAndChannelTagDetectFilterRegex();

    [GeneratedRegex("\\d+")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(PromptSpoofDetectionRegexStr)]
    private static partial Regex PromptSpoofDetectionRegex();
}