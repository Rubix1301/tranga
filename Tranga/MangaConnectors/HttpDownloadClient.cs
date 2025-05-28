using System.Net;
using System.Net.Http.Headers;
using HtmlAgilityPack;

namespace Tranga.MangaConnectors;

internal class HttpDownloadClient : DownloadClient
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public HttpDownloadClient(GlobalBase clone) : base(clone)
    {
        Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", TrangaSettings.userAgent);
    }

    internal override RequestResult MakeRequestInternal(string url, string? referrer = null, string? clickButton = null)
    {
        if (clickButton is not null)
            Log("Cannot click button on static site.");

        HttpResponseMessage? response = null;

        while (response is null)
        {
            HttpRequestMessage requestMessage = new(HttpMethod.Get, url);

            requestMessage.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
            requestMessage.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            requestMessage.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
            requestMessage.Headers.Referrer = new Uri("https://natomanga.com/");
            requestMessage.Headers.Connection.ParseAdd("keep-alive");
            requestMessage.Headers.Add("Upgrade-Insecure-Requests", "1");

            if (referrer != null)
                requestMessage.Headers.Referrer = new Uri(referrer);

            try
            {
                response = Client.Send(requestMessage);
            }
            catch (Exception e)
            {
                switch (e)
                {
                    case TaskCanceledException:
                        Log($"Request timed out {url}.\n{e}");
                        return new RequestResult(HttpStatusCode.RequestTimeout, null, Stream.Null);
                    case HttpRequestException:
                        Log($"Request failed {url}\n{e}");
                        return new RequestResult(HttpStatusCode.BadRequest, null, Stream.Null);
                }
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            Log($"Request-Error {response.StatusCode}: {url}");
            return new RequestResult(response.StatusCode, null, Stream.Null);
        }

        // Read the full content as string (works for any content-type)
        string contentString = response.Content.ReadAsStringAsync().Result;

        HtmlDocument? document = null;
        if (response.Content.Headers.ContentType?.MediaType?.Contains("html") == true)
        {
            document = new HtmlDocument();
            document.LoadHtml(contentString);
        }

        // Create a seekable stream for downstream consumption
        Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(contentString));

        return new RequestResult(response.StatusCode, document, stream, true,
            response.RequestMessage?.RequestUri?.AbsoluteUri);
    }
}