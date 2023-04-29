using System.IO;
using System.Net.Http;

namespace Zintom.OpenAIWrapper;
public class HttpClientWrapper : HttpClient, IHttpClient
{
    public void GetStreamingResponse(HttpRequestMessage request, out HttpResponseMessage response, out Stream responseStream)
    {
        response = Send(request, HttpCompletionOption.ResponseHeadersRead);
        responseStream = response.Content.ReadAsStream();
    }
}
