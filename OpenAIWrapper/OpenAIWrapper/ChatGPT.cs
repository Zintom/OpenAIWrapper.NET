using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Zintom.OpenAIWrapper.Models;

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
    }

    /// <summary>
    /// Creates a new instance of the ChatCompletions API wrapper.
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="client"></param>
    public ChatGPT(string? apiKey, IHttpClient? client)
    {
        _client = client ?? new HttpClientWrapper();
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


        API_Key = apiKey;
    }

    private string InternalCreateRequestJson(List<Message> messages, ChatCompletionOptions? options = null, bool streamResponse = false, params FunctionDefinition[]? functions)
    {
        options ??= _defaultChatCompletionOptions;

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

    private void InternalHandleFunctionCall(List<Message> messages, ChatCompletion chatCompletion, FunctionDefinition[] functions, ChatCompletionOptions? options)
    {
        FunctionCall? modelFunctionCall = chatCompletion?.Choices?[0].Message?.FunctionCall;
        if (modelFunctionCall == null)
        {
            _logger.Debug("Model provided a function call that was null.");
            return;
        }

        _logger.Debug($"Function call requested '{chatCompletion?.Choices?[0].Message?.FunctionCall?.Name}'" +
            $"\nArgs: {string.Join(",", modelFunctionCall.Arguments.Select((definition) => $"\n--> {{ Name: '{definition.Name}', Value: '{definition.Value}', Type: '{definition.Type}' }}"))}");

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
        while (true)
        {
            string requestBody = InternalCreateRequestJson(messages, options, false, functions);

            using HttpResponseMessage response = await _client.PostAsync(_requestUri, new StringContent(requestBody, Encoding.UTF8, "application/json"));

            string content = await response.Content.ReadAsStringAsync();

            var chatCompletion = JsonSerializer.Deserialize<ChatCompletion>(content);

            // If the model wants to execute a function, we handle the entire function process before
            // returning back to the user.

            if (chatCompletion?.Choices?[0].FinishReason == "function_call"
                && functions != null)
            {
                // Prevent infinite loops by supressing any further function calls
                // If the model has called the same function and has got the same response twice.
                if (messages.Count >= 2)
                {
                    var lastMessage = messages[^1];
                    var secondToLastMessage = messages[^2];
                    if (lastMessage.Role == "function" && secondToLastMessage.Role == "function" &&
                        lastMessage.Name == secondToLastMessage.Name &&
                        lastMessage.Content == secondToLastMessage.Content)
                    {
                        // This function was called identically already which means there is an infinite loop.
                        messages.Add(new Message() { Role = "assistant", Content = "[Internal Thought] I've already called that function and have had the same response back, there must be an error somewhere." });

                        // Set the available functions to null so that the AI can't call any more for this completion.
                        functions = null;
                        continue;
                    }
                }

                InternalHandleFunctionCall(messages, chatCompletion, functions, options);
            }
            else
            {
                return chatCompletion;
            }
        }
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
