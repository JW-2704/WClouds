using System.Net;
using System.Net.Http;
using System.Text.Json;
using RichardSzalay.MockHttp;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

// AI Agent: ShareFile(MemberIDs: List<int>, ...) -> ShareFile(MemberID: int,
// WrappedKey: string, ...) - jeder Empfaenger braucht einen individuell mit
// seinem Public Key gewrappten DEK, ShareDialog teilt ohnehin nur an einen
// Empfaenger pro Durchlauf.
public class ShareServiceTests : WebserviceTestBase
{
    private readonly ShareService _sut = new();

    [Fact]
    public async Task ShareFile_ValidRequest_CompletesWithoutException()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/share/file")
            .Respond(HttpStatusCode.OK);

        // Should not throw
        await _sut.ShareFile(
            MemberID: 1,
            WrappedKey: "wrapped-dek-blob",
            CanRead: true,
            CanWrite: false,
            FileID: 10
        );
    }

    [Fact]
    public async Task ShareFile_SendsCorrectJson()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Post, "http://localhost/share/file")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK);

        await _sut.ShareFile(
            MemberID: 5,
            WrappedKey: "wrapped-dek-blob",
            CanRead: true,
            CanWrite: true,
            FileID: 99
        );

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;

        Assert.Equal(99, root.GetProperty("fileId").GetInt32());
        Assert.True(root.GetProperty("canRead").GetBoolean());
        Assert.True(root.GetProperty("canWrite").GetBoolean());

        var grants = root.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal(5, grants[0].GetProperty("memberId").GetInt32());
        Assert.Equal("wrapped-dek-blob", grants[0].GetProperty("wrappedKey").GetString());
    }

    [Fact]
    public async Task ShareFile_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/share/file")
            .Respond(HttpStatusCode.Forbidden);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.ShareFile(1, "wrapped-dek-blob", true, false, 1)
        );
    }

    [Fact]
    public async Task ShareFile_ReadOnlyPermission_SentCorrectly()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Post, "http://localhost/share/file")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK);

        await _sut.ShareFile(1, "wrapped-dek-blob", CanRead: true, CanWrite: false, FileID: 3);

        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.True(doc.RootElement.GetProperty("canRead").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("canWrite").GetBoolean());
    }
}
