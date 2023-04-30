using System.IO;
using System.Net.Http;

namespace Zintom.OpenAIWrapper;

/// <summary>
/// A wrapper around the <see cref="HttpClient"/>, implements <see cref="IHttpClient"/>.
/// </summary>
public sealed class HttpClientWrapper : HttpClient, IHttpClient
{
    /// <inheritdoc cref="IHttpClient.GetStreamingResponse(HttpRequestMessage, out HttpResponseMessage, out Stream)"/>
    public void GetStreamingResponse(HttpRequestMessage request, out HttpResponseMessage response, out Stream responseStream)
    {
        response = Send(request, HttpCompletionOption.ResponseHeadersRead);
        responseStream = response.Content.ReadAsStream();
    }
}
