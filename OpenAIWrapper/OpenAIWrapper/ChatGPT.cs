using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Zintom.OpenAIWrapper.Models;
using static Zintom.OpenAIWrapper.Models.FunctionCall;

namespace Zintom.OpenAIWrapper;

/// <summary>
/// A wrapper around the ChatCompletions API.
/// </summary>
public sealed class ChatGPT
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly IHttpClient _client;

    private const string _requestUri = "https://api.openai.com/v1/chat/completions";

    /// <summary>
    /// Sets the API key to be used for future requests made by this instance.
    /// </summary>
    public string? API_Key
    {
        set
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
        }
    }

    private readonly ChatCompletionOptions _defaultChatCompletionOptions = new();

    /// <summary>
    /// Configurable options for a ChatCompletion API call.
    /// </summary>
    public sealed class ChatCompletionOptions
    {
        /// <summary>
        /// The ID of the model to use. See the <see href="https://platform.openai.com/docs/models/model-endpoint-compatibility">model endpoint compatibility</see> table for details on which models work with the Chat API.
        /// <para/>
        /// Also see <see cref="LanguageModels"/>.
        /// </summary>
        public string? Model = "gpt-3.5-turbo";

        /// <summary>
        /// The 'temperature' parameter to be used for future requests made by this instance.
        /// </summary>
        public float Temperature = 0.7f;

        /// <summary>
        /// If set to <see langword="true"/>, does not stop the model from calling the same function over and over again with the <i>same</i> arguments if it wants to.
        /// </summary>
        /// <remarks>
        /// The default is <see langword="false"/>, this is because sometimes the model will 
        /// almost 'ignore' the result of a function call and keep making the same call (expecting a different response), this
        /// could result in a massive cost to the user/developer as an infinite request loop consumes lots of tokens.
        /// </remarks>
        public bool AllowInfiniteFunctionCalls = false;
    }

    /// <summary>
    /// Creates a new instance of the ChatCompletions API wrapper.
    /// </summary>
    /// <param name="apiKey">Your OpenAI API key.</param>
    /// <param name="client">Leave as <see langword="null"/> and it will create one for you.</param>
    public ChatGPT(string? apiKey, IHttpClient? client = null)
    {
        _client = client ?? new HttpClientWrapper();
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


        API_Key = apiKey;
    }

    private static string InternalCreateRequestJson(List<Message> messages, ChatCompletionOptions options, bool streamResponse = false, params FunctionDefinition[]? functions)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = false });

        writer.WriteStartObject();

        writer.WriteString("model", options.Model);

        string messagesJson = JsonSerializer.Serialize(messages, new JsonSerializerOptions() { WriteIndented = false });
        writer.WritePropertyName("messages");
        writer.WriteRawValue(messagesJson);
        writer.WriteNumber("temperature", options.Temperature);
        writer.WriteBoolean("stream", streamResponse);

        if (functions != null && functions.Length > 0)
        {
            writer.WriteStartArray("functions");
            for (int i = 0; i < functions.Length; i++)
            {
                writer.WriteRawValue(functions[i].ToJsonSchema());
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();

        writer.Flush();

        using (var sr = new StreamReader(stream))
        {
            stream.Position = 0;
            return sr.ReadToEnd();
        }
    }

    private void InternalHandleFunctionCall(List<Message> messages, ChatCompletion chatCompletion, FunctionDefinition[] functions)
    {
        FunctionCall? modelFunctionCall = chatCompletion?.Choices?[0].Message?.FunctionCall;
        if (modelFunctionCall == null)
        {
            _logger.Debug("Model provided a function call that was null.");
            return;
        }

        _logger.Debug($"Function call requested '{chatCompletion?.Choices?[0].Message?.FunctionCall?.Name}'" +
            $"\nArgs: {string.Join(",", modelFunctionCall.Arguments.Select((definition) => $"\n--> {{ Name: '{definition.Name}', Value: '{Encoding.UTF8.GetString(definition.RawValue)}', Type: '{definition.Type}' }}"))}");

        // ENSURE ENUM VALUES ARE VALID!

        foreach (var functionDefinition in functions)
        {
            if (functionDefinition.Name == modelFunctionCall.Name)
            {
                // Execute the requested function.
                string? functionResult = functionDefinition.RunFunction(modelFunctionCall.Arguments);
                _logger.Debug($"Function result: '{functionResult}'");

                // Append the function response to the message history.
                messages.Add(new Message() { Role = "function", Name = modelFunctionCall.Name, Content = functionResult });
                return;
            }
        }

        _logger.Debug("Requested function not found.");
    }


    /// <inheritdoc cref="GetChatCompletion(List{Message}, ChatCompletionOptions?, FunctionDefinition[])"/>
    public Task<ChatCompletion?> GetChatCompletion(List<Message> messages,
                                                         ChatCompletionOptions? options = null,
                                                         params Delegate[]? functions)
    {
        return GetChatCompletion(messages,
                                 options,
                                 functions != null ? Array.ConvertAll(functions, new Converter<Delegate, FunctionDefinition>(FunctionDefinition.FromMethod)) : null);
    }

    /// <summary>
    /// Gets a chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <param name="options">Options used to configure the API call.</param>
    /// <param name="functions">Any functions you wish for the GPT model to be able to call.</param>
    /// <returns>A <see cref="ChatCompletion"/> for the conversation history provided in the <paramref name="messages"/> array.</returns>
    public async Task<ChatCompletion?> GetChatCompletion(List<Message> messages,
                                                     ChatCompletionOptions? options = null,
                                                     params FunctionDefinition[]? functions)
    {
        options ??= _defaultChatCompletionOptions;

        if (options.AllowInfiniteFunctionCalls)
        {
            _logger.Warn($"Warning! {nameof(options.AllowInfiniteFunctionCalls)} is TRUE. Therefore the model can make duplicate function calls and may find itself stuck in a loop.");
        }

        List<FunctionCall>? _functionCallHistory = null;

        while (true)
        {
            string requestBody = InternalCreateRequestJson(messages, options, false, functions);

            using HttpResponseMessage response = await _client.PostAsync(_requestUri, new StringContent(requestBody, Encoding.UTF8, "application/json"));

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI responded with status code '{response.StatusCode} ({(int)response.StatusCode})', Message: '{content}'");
            }

            var chatCompletion = JsonSerializer.Deserialize<ChatCompletion>(content) ?? throw new Exception("ChatCompletion could not be deserialized.");

            // KEEP TRACK OF EXECUTED FUNCTIONS.

            var choice0 = chatCompletion?.Choices?[0] ?? throw new UnreachableException();

            // If the model wants to execute a function, we handle the entire function process before
            // returning back to the user.

            if (choice0.FinishReason == "function_call"
                && functions != null)
            {
                _functionCallHistory ??= new();

                if (choice0.Message?.FunctionCall != null)
                    _functionCallHistory.Add(choice0.Message.FunctionCall);

                if (options.AllowInfiniteFunctionCalls == false &&
                    HasDuplicateFunctionCalls(_functionCallHistory))
                {
                    _logger.Debug($"Duplicate function call made by the model '{choice0.Message?.FunctionCall?.Name}'");

                    // This function was called identically already which means there is an infinite loop.
                    messages.Add(new Message() { Role = "assistant", Content = "[INTERNAL THOUGHT] I have already called that function with the same parameters. I have made an error, not the user." });

                    // Set the available functions to null so that the AI can't call any more for this completion.
                    functions = null;
                    continue;
                }

                InternalHandleFunctionCall(messages, chatCompletion, functions);
            }
            else
            {
                return chatCompletion;
            }
        }
    }

    /// <summary>
    /// Identifies duplicate <see cref="FunctionCall"/> objects in the given list <paramref name="functionCalls"/>.
    /// </summary>
    private static bool HasDuplicateFunctionCalls(List<FunctionCall>? functionCalls)
    {
        // If the list of functionCalls is null or contains only one item, there can be no duplicates,
        // thus returns false.
        if (functionCalls == null || functionCalls.Count <= 1)
        {
            return false;
        }

        // For comparison purpose, saves the name and arguments of the first function call.
        string? lastFunctionName = functionCalls[0].Name;
        List<ArgumentDefinition>? lastFunctionArgs = functionCalls[0].Arguments;

        // Iterates over the remaining function calls in the list.
        for (int i = 1; i < functionCalls.Count; i++)
        {
            var function = functionCalls[i];

            // Checks if the current function call has the same name and the same number of arguments
            // as the last function call.
            if (function.Name == lastFunctionName &&
                function.Arguments.Count == lastFunctionArgs?.Count)
            {
                // If same name and argument count are found, iterates over each argument.
                for (int a = 0; a < function.Arguments.Count; a++)
                {
                    // Compares each argument's raw value; if they are the same, returns true,
                    // indicating a duplicate function call has been found.
                    if (function.Arguments[a].RawValue.SequenceEqual(lastFunctionArgs[a].RawValue))
                    {
                        return true;
                    }
                }
            }

            // If no duplicate found for the current function, saves its information 
            // for comparison with the next function call in the list.
            lastFunctionName = function.Name;
            lastFunctionArgs = function.Arguments;
        }

        // If no duplicates are found after scanning the entire list, returns false.
        return false;
    }

    /// <summary>
    /// Gets a streaming chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <param name="partialCompletionCallback">The callback function for each time a 'delta' <see cref="ChatCompletion"/> is received.</param>
    /// <param name="options">Options used to configure the API call.</param>
    /// <param name="functions">Any functions you wish for the GPT model to be able to call.</param>
    /// <returns>A <see cref="HttpStatusCode"/> which represents the response to the <i>POST</i> message.</returns>
    public HttpStatusCode GetStreamingChatCompletion(List<Message> messages,
                                                     Action<ChatCompletion?> partialCompletionCallback,
                                                     ChatCompletionOptions? options = null,
                                                     params FunctionDefinition[]? functions)
    {
        if (functions != null)
            throw new NotImplementedException("Function calls do not currently work with streaming output.");

        options ??= _defaultChatCompletionOptions;

        string requestBody = InternalCreateRequestJson(messages, options, true, functions);

        _client.GetStreamingResponse(new HttpRequestMessage(HttpMethod.Post, new Uri(_requestUri)) { Content = new StringContent(requestBody, Encoding.UTF8, "application/json") },
                                     out HttpResponseMessage response,
                                     out Stream responseStream);

        if (!response.IsSuccessStatusCode)
            return response.StatusCode;

        // Read event stream
        // https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events#event_stream_format

        Span<byte> buffer = new byte[1024 * 1024 * 10]; // 10 MB buffer

        int totalBytesRead = 0;
        int bytesRead;
        int streamPosition = 0;
        while ((bytesRead = responseStream.Read(buffer[totalBytesRead..])) > 0)
        {
            totalBytesRead += bytesRead;

            while (TryConsume(buffer[streamPosition..totalBytesRead], out int consumed, out ChatCompletion? completion))
            {
                streamPosition += consumed;

                partialCompletionCallback.Invoke(completion);
            }
        }

        return response.StatusCode;

        static bool TryConsume(Span<byte> buf, out int consumed, out ChatCompletion? completion)
        {
            var dataTextUtf8 = "data: "u8;
            var doneTextUtf8 = "data: [DONE]\n\n"u8;

            // Ensure we are dealing with a data line.
            if (buf.Length < dataTextUtf8.Length ||
                !buf[..dataTextUtf8.Length].SequenceEqual(dataTextUtf8))
            {
                goto exit;
            }

            // The [DONE] message is an indication from the OpenAI API that the completion has finished.
            if (buf.Length >= doneTextUtf8.Length &&
                buf[..doneTextUtf8.Length].SequenceEqual(doneTextUtf8))
            {
                goto exit;
            }

            // Identify double newline chars, they represent the end of the data line.
            for (int i = 1; i < buf.Length; i++)
            {
                byte precedingValue = buf[i - 1];
                byte currentValue = buf[i];

                if (precedingValue == 0x0A && // 0x0A == \n
                    currentValue == 0x0A)
                {
                    var dataLine = buf[dataTextUtf8.Length..(i + 1)];
                    consumed = i + 1;
                    completion = JsonSerializer.Deserialize<ChatCompletion>(dataLine);
                    return true;
                }
            }

        exit:
            consumed = 0;
            completion = null;
            return false;
        }
    }

}
