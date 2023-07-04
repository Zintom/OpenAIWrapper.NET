using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Zintom.OpenAIWrapper.Models;

namespace Zintom.OpenAIWrapper;

public sealed class ChatGPT
{
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

    public sealed class ChatCompletionOptions
    {
        /// <summary>
        /// The ID of the model to use. See the <see href="https://platform.openai.com/docs/models/model-endpoint-compatibility">model endpoint compatibility</see> table for details on which models work with the Chat API.
        /// </summary>
        public string? Model = "gpt-3.5-turbo";

        /// <summary>
        /// The 'temperature' parameter to be used for future requests made by this instance.
        /// </summary>
        public float Temperature = 0.7f;
    }

    public ChatGPT(string? apiKey, IHttpClient? client)
    {
        _client = client ?? new HttpClientWrapper();
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


        API_Key = apiKey;
    }

    /// <summary>
    /// Gets a chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <param name="options">Options used to configure the API call.</param>
    /// <returns>A <see cref="ChatCompletion"/> for the conversation history provided in the <paramref name="messages"/> array.</returns>
    public async Task<ChatCompletion?> GetChatCompletion(Message[] messages, ChatCompletionOptions? options = null)
    {
        options ??= _defaultChatCompletionOptions;

        var requestBodyObject = new
        {
            // Do not try to optimize names here, the API needs the specific lower case names.
            model = options.Model,
            messages,
            temperature = options.Temperature
        };

        string requestBody = JsonSerializer.Serialize(requestBodyObject);

        using HttpResponseMessage response = await _client.PostAsync(_requestUri, new StringContent(requestBody, Encoding.UTF8, "application/json"));

        return JsonSerializer.Deserialize<ChatCompletion>(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Gets a chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <param name="options">Options used to configure the API call.</param>
    /// <returns>A <see cref="ChatCompletion"/> for the conversation history provided in the <paramref name="messages"/> array.</returns>
    public async Task<ChatCompletion?> GetChatCompletionFunc(Message[] messages, string functionsJsonSchema, ChatCompletionOptions? options = null)
    {
        options ??= _defaultChatCompletionOptions;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true });

        writer.WriteStartObject();

        writer.WriteString("model", options.Model);

        string messagesJson = JsonSerializer.Serialize(messages, new JsonSerializerOptions() { WriteIndented = true });
        writer.WritePropertyName("messages");
        writer.WriteRawValue(messagesJson);

        writer.WriteNumber("temperature", options.Temperature);

        writer.WritePropertyName("functions");
        writer.WriteRawValue(functionsJsonSchema);

        writer.WriteEndObject();

        writer.Flush();

        using (var sr = new StreamReader(stream))
        {
            stream.Position = 0;
            string requestBody = sr.ReadToEnd();

            using HttpResponseMessage response = await _client.PostAsync(_requestUri, new StringContent(requestBody, Encoding.UTF8, "application/json"));

            string content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ChatCompletion>(content);
        }
    }

    /// <summary>
    /// Gets a streaming chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <param name="partialCompletionCallback">The callback function for each time a 'delta' <see cref="ChatCompletion"/> is received.</param>
    /// <param name="options">Options used to configure the API call.</param>
    /// <returns>A <see cref="HttpStatusCode"/> which represents the response to the <i>POST</i> message.</returns>
    public HttpStatusCode GetStreamingChatCompletion(Message[] messages, Action<ChatCompletion?> partialCompletionCallback, ChatCompletionOptions? options = null)
    {
        options ??= _defaultChatCompletionOptions;

        var requestBodyObject = new
        {
            // Do not try to optimize names here, the API needs the specific lower case names.
            model = options.Model,
            messages,
            temperature = options.Temperature,
            stream = true
        };

        string requestBody = JsonSerializer.Serialize(requestBodyObject);

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