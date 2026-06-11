using System.Net.Http;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

public class WebserviceTests
{
    [Fact]
    public void SetApiKey_StoresKeyAndAddsHeader()
    {
        // Save original state
        var originalClient = Webservice.HttpClient;
        try
        {
            Webservice.HttpClient = new HttpClient();
            Webservice.SetApiKey("test-api-key-123");

            Assert.Equal("test-api-key-123", Webservice.APIKey);
            Assert.Contains(
                Webservice.HttpClient.DefaultRequestHeaders,
                h => h.Key == "X-API-Key" && h.Value.Contains("test-api-key-123")
            );
        }
        finally
        {
            Webservice.HttpClient = originalClient;
        }
    }

    [Fact]
    public void SetApiKey_CalledTwice_ReplacesOldHeader()
    {
        var originalClient = Webservice.HttpClient;
        try
        {
            Webservice.HttpClient = new HttpClient();
            Webservice.SetApiKey("first-key");
            Webservice.SetApiKey("second-key");

            Assert.Equal("second-key", Webservice.APIKey);

            // Only one X-API-Key header should exist
            int count = 0;
            foreach (var h in Webservice.HttpClient.DefaultRequestHeaders)
                if (h.Key == "X-API-Key") count++;

            Assert.Equal(1, count);
            Assert.Contains(
                Webservice.HttpClient.DefaultRequestHeaders,
                h => h.Key == "X-API-Key" && h.Value.Contains("second-key")
            );
        }
        finally
        {
            Webservice.HttpClient = originalClient;
        }
    }
}
