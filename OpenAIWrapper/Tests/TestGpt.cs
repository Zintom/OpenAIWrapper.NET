using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Zintom.OpenAIWrapper;

namespace Tests;

[TestClass]
public class TestGpt
{
    [TestMethod]
    public void StreamingResponse()
    {
        // Arrange
        var client = new MockHttpClient();
        var gpt = new ChatGPT(Environment.GetEnvironmentVariable("API_KEY"), client);
        var finalStringBuilder = new StringBuilder();

        // Act
        gpt.GetStreamingChatCompletion(new Zintom.OpenAIWrapper.Models.Message[] { new Zintom.OpenAIWrapper.Models.Message() { Role = "user", Content = "Hello!" } }, (cp) =>
        {
            if (string.IsNullOrEmpty(cp!.Choices![0].Delta?.Role))
            {
                finalStringBuilder.Append(cp!.Choices![0].Delta!.Content);
            }
            else
            {
                finalStringBuilder.Append($"Role: {cp!.Choices![0].Delta?.Role}, Message: ");
            }
        });

        // Assert
        Assert.IsTrue(finalStringBuilder.ToString() == "Role: assistant, Message: Hello there!");
    }

}

file class MockHttpClient : IHttpClient
{
    public HttpRequestHeaders DefaultRequestHeaders { get; } = new HttpClient().DefaultRequestHeaders;

    public void GetStreamingResponse(HttpRequestMessage request, out HttpResponseMessage response, out Stream responseStream)
    {
        response = new HttpResponseMessage(HttpStatusCode.OK) { Content = null };
        responseStream = new SlowMemoryStream(Encoding.UTF8.GetBytes("data: {\"id\":\"chatcmpl-7AnQVkmoqjTrJqnwJ3it0iJP11kIo\",\"object\":\"chat.completion.chunk\",\"created\":1682807631,\"model\":\"gpt-3.5-turbo-0301\",\"choices\":[{\"delta\":{\"role\":\"assistant\"},\"index\":0,\"finish_reason\":null}]}\n\ndata: {\"id\":\"chatcmpl-7AnQVkmoqjTrJqnwJ3it0iJP11kIo\",\"object\":\"chat.completion.chunk\",\"created\":1682807631,\"model\":\"gpt-3.5-turbo-0301\",\"choices\":[{\"delta\":{\"content\":\"Hello\"},\"index\":0,\"finish_reason\":null}]}\n\ndata: {\"id\":\"chatcmpl-7AnQVkmoqjTrJqnwJ3it0iJP11kIo\",\"object\":\"chat.completion.chunk\",\"created\":1682807631,\"model\":\"gpt-3.5-turbo-0301\",\"choices\":[{\"delta\":{\"content\":\" there\"},\"index\":0,\"finish_reason\":null}]}\n\ndata: {\"id\":\"chatcmpl-7AnQVkmoqjTrJqnwJ3it0iJP11kIo\",\"object\":\"chat.completion.chunk\",\"created\":1682807631,\"model\":\"gpt-3.5-turbo-0301\",\"choices\":[{\"delta\":{\"content\":\"!\"},\"index\":0,\"finish_reason\":null}]}\n\ndata: {\"id\":\"chatcmpl-7AnQVkmoqjTrJqnwJ3it0iJP11kIo\",\"object\":\"chat.completion.chunk\",\"created\":1682807631,\"model\":\"gpt-3.5-turbo-0301\",\"choices\":[{\"delta\":{},\"index\":0,\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n"), maxRead: 16, readLatency: 0);
    }

    public Task<HttpResponseMessage> PostAsync([StringSyntax("Uri")] string? requestUri, HttpContent? content)
    {
        throw new System.NotImplementedException();
    }

}