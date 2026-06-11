using System.Net;
using System.Net.Http;
using System.Text.Json;
using RichardSzalay.MockHttp;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

public class StorageServiceTests : WebserviceTestBase
{
    private readonly StorageService _sut = new();

    // ── GetFile ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFile_ValidId_ReturnsSavedFile()
    {
        var payload = JsonSerializer.Serialize(new { ID = 7, FileName = "photo.png", Extension = ".png" });

        MockHttp
            .When(HttpMethod.Get, "http://localhost/files/info/7")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        SavedFile? result = await _sut.GetFile(7);

        Assert.NotNull(result);
        Assert.Equal(7, result!.ID);
        Assert.Equal("photo.png", result.FileName);
    }

    [Fact]
    public async Task GetFile_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Get, "http://localhost/files/info/99")
            .Respond(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.GetFile(99));
    }

    // ── GetDirectory ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDirectory_ValidId_ReturnsSavedDirectory()
    {
        var payload = JsonSerializer.Serialize(new { ID = 3, Name = "Documents" });

        MockHttp
            .When(HttpMethod.Get, "http://localhost/directories/3")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        SavedDirectory? result = await _sut.GetDirectory(3);

        Assert.NotNull(result);
        Assert.Equal(3, result!.ID);
        Assert.Equal("Documents", result.Name);
    }

    // ── UploadFile ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFile_SendsMultipartRequest_ReturnsDeserializedFile()
    {
        string? contentType = null;
        var responsePayload = JsonSerializer.Serialize(new { ID = 55, FileName = "doc.txt", Extension = ".txt" });

        MockHttp
            .When(HttpMethod.Post, "http://localhost/files/")
            .With(req =>
            {
                contentType = req.Content?.Headers.ContentType?.MediaType;
                return true;
            })
            .Respond(HttpStatusCode.OK, "application/json", responsePayload);

        var file = new SavedFile
        {
            ID = 0,
            FileName = "doc.txt",
            Extension = ".txt",
            Content = System.Text.Encoding.UTF8.GetBytes("hello world")
        };

        SavedFile? result = await _sut.UploadFile(file, ownerId: 1);

        Assert.NotNull(result);
        Assert.Equal(55, result!.ID);
        Assert.Equal("multipart/form-data", contentType);
    }

    [Fact]
    public async Task UploadFile_NullContent_ThrowsArgumentException()
    {
        var file = new SavedFile { ID = 1, FileName = "empty.txt", Content = null };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UploadFile(file, 1));
    }

    // ── DownloadFile ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadFile_WithNonceHeader_ReturnsDecryptedBytes()
    {
        byte[] original = System.Text.Encoding.UTF8.GetBytes("file content");
        var (encrypted, nonce) = EncryptionService.Encrypt(original);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(encrypted)
        };
        response.Headers.Add("X-Nonce", nonce);

        MockHttp
            .When(HttpMethod.Get, "http://localhost/files/download/10")
            .Respond(_ => Task.FromResult(response));

        byte[]? result = await _sut.DownloadFile(10);

        Assert.NotNull(result);
        Assert.Equal(original, result);
    }

    [Fact]
    public async Task DownloadFile_MissingNonceHeader_Throws()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };

        MockHttp
            .When(HttpMethod.Get, "http://localhost/files/download/20")
            .Respond(_ => Task.FromResult(response));

        await Assert.ThrowsAsync<Exception>(() => _sut.DownloadFile(20));
    }

    // ── UploadDirectory ──────────────────────────────────────────────────────

    [Fact]
    public async Task UploadDirectory_ValidPath_ReturnsDirectory()
    {
        string? capturedBody = null;
        var payload = JsonSerializer.Serialize(new { ID = 8, Name = "NewFolder" });

        MockHttp
            .When(HttpMethod.Post, "http://localhost/directories")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK, "application/json", payload);

        SavedDirectory? result = await _sut.UploadDirectory("/home/user/NewFolder");

        Assert.NotNull(result);
        Assert.Equal(8, result!.ID);
        Assert.Contains("/home/user/NewFolder", capturedBody);
    }
}