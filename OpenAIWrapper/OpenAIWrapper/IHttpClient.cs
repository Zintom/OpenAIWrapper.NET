using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Zintom.OpenAIWrapper;

/// <summary>
/// Exposes <see cref="HttpClient"/> as an interface.
/// </summary>
public interface IHttpClient
{

    /// <inheritdoc cref="HttpClient.DefaultRequestHeaders"/>
    public HttpRequestHeaders DefaultRequestHeaders { get; }

    /// <inheritdoc cref="HttpClient.PostAsync(string?, HttpContent?)"/>
    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content);

    /// <summary>
    /// Sends the given <paramref name="request"/> and gets the response data as a stream.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <param name="responseStream"></param>
    public void GetStreamingResponse(HttpRequestMessage request, out HttpResponseMessage response, out Stream responseStream);

}
