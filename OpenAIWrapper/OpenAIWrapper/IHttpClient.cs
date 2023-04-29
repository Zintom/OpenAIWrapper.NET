using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Zintom.OpenAIWrapper;

public interface IHttpClient
{

    public HttpRequestHeaders DefaultRequestHeaders { get; }

    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content);

    // Intended to wrap the 'HttpClient.Send' function, allowing mocking.
    public void GetStreamingResponse(HttpRequestMessage request, out HttpResponseMessage response, out Stream responseStream);

}
