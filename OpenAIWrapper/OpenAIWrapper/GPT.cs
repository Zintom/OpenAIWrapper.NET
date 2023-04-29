using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Zintom.OpenAIWrapper.Models;

namespace Zintom.OpenAIWrapper;

public class GPT
{
    private readonly IHttpClient _client;

    private const string _requestUri = "https://api.openai.com/v1/chat/completions";

    /// <summary>
    /// Sets the API key to be used for future requests made by this instance.
    /// </summary>
    public string? APIKey
    {
        set
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
        }
    }

    public string? Model = "gpt-3.5-turbo";

    /// <summary>
    /// The 'temperature' parameter to be used for future requests made by this instance.
    /// </summary>
    public float Temperature = 0.7f;

    public GPT(string? apiKey, IHttpClient? client)
    {
        _client = client ?? new HttpClientWrapper();
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


        APIKey = apiKey;
    }

    /// <summary>
    /// Gets a chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <returns>A <see cref="ChatCompletion"/> for the conversation history provided in the <paramref name="messages"/> array.</returns>
    public async Task<ChatCompletion?> GetChatCompletion(Message[] messages)
    {
        var requestBodyObject = new
        {
            // Do not try to optimize names here, the API needs the specific lower case names.
            model = Model,
            messages,
            temperature = Temperature
        };

        string requestBody = JsonSerializer.Serialize(requestBodyObject);

        using HttpResponseMessage response = await _client.PostAsync(_requestUri, new StringContent(requestBody, Encoding.UTF8, "application/json"));

        return JsonSerializer.Deserialize<ChatCompletion>(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Gets a chat completion for the conversation history provided in <paramref name="messages"/>.
    /// </summary>
    /// <param name="messages">A list of messages describing the conversation so far.</param>
    /// <returns>A <see cref="ChatCompletion"/> for the conversation history provided in the <paramref name="messages"/> array.</returns>
    public void GetStreamingChatCompletion(Message[] messages, Action<ChatCompletion?> partialCompletionCallback)
    {
        var requestBodyObject = new
        {
            // Do not try to optimize names here, the API needs the specific lower case names.
            model = Model,
            messages,
            temperature = Temperature,
            stream = true
        };

        string requestBody = JsonSerializer.Serialize(requestBodyObject);

        _client.GetStreamingResponse(new HttpRequestMessage(HttpMethod.Post, new Uri(_requestUri)) { Content = new StringContent(requestBody, Encoding.UTF8, "application/json") },
                                     out HttpResponseMessage response,
                                     out Stream responseStream);

        if (!response.IsSuccessStatusCode)
            return;

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