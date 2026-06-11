using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RichardSzalay.MockHttp;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

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
            MemberIDs: new List<int> { 1, 2, 3 },
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
            MemberIDs: new List<int> { 5, 6 },
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

        var members = root.GetProperty("memberIds");
        Assert.Equal(2, members.GetArrayLength());
    }

    [Fact]
    public async Task ShareFile_EmptyMemberList_StillPostsSuccessfully()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/share/file")
            .Respond(HttpStatusCode.OK);

        await _sut.ShareFile(
            MemberIDs: new List<int>(),
            CanRead: false,
            CanWrite: false,
            FileID: 1
        );
    }

    [Fact]
    public async Task ShareFile_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/share/file")
            .Respond(HttpStatusCode.Forbidden);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.ShareFile(new List<int> { 1 }, true, false, 1)
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

        await _sut.ShareFile(new List<int> { 1 }, CanRead: true, CanWrite: false, FileID: 3);

        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.True(doc.RootElement.GetProperty("canRead").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("canWrite").GetBoolean());
    }
}
