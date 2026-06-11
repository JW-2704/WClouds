using System.Net;
using System.Net.Http;
using RichardSzalay.MockHttp;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

/// <summary>
/// Replaces Webservice.HttpClient with a MockHttpMessageHandler-backed client
/// and resets it after each test via IDisposable.
/// </summary>
public abstract class WebserviceTestBase : IDisposable
{
    protected readonly MockHttpMessageHandler MockHttp;
    private readonly HttpClient _originalClient;

    protected WebserviceTestBase()
    {
        _originalClient = Webservice.HttpClient;

        MockHttp = new MockHttpMessageHandler();
        Webservice.HttpClient = MockHttp.ToHttpClient();
        Webservice.HttpClient.BaseAddress = new Uri("http://localhost");
    }

    public void Dispose()
    {
        Webservice.HttpClient = _originalClient;
    }

    // Convenience helpers -------------------------------------------------

    protected static StringContent Json(string json) =>
        new StringContent(json, System.Text.Encoding.UTF8, "application/json");

    protected static HttpResponseMessage OkJson(string json) =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}
