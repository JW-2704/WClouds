using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RichardSzalay.MockHttp;
using Xunit;
using System.Linq;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

// ── Info record ──────────────────────────────────────────────────────────────

public class InfoTests
{
    [Fact]
    public void Info_ConstructorSetsAllProperties()
    {
        var info = new Info(
            "2024-06-01",
            "14:30:00",
            1024.5,
            "2",
            "1",
            "report.pdf");

        Assert.Equal("2024-06-01", info.ChangedDate);
        Assert.Equal("14:30:00", info.ChangedTime);
        Assert.Equal(1024.5, info.Size);
        Assert.Equal("2", info.ChangedUser);
        Assert.Equal("1", info.Owner);
        Assert.Equal("report.pdf", info.Name);
    }

    [Fact]
    public void Info_EqualityByValue()
    {
        var a = new Info(
            "2024-01-01",
            "12:00:00",
            10,
            "1",
            "2",
            "file.txt");

        var b = new Info(
            "2024-01-01",
            "12:00:00",
            10,
            "1",
            "2",
            "file.txt");

        Assert.Equal(a, b);
    }
}

// ── LoginResponse ─────────────────────────────────────────────────────────────

public class LoginResponseTests
{
    [Fact]
    public void LoginResponse_DefaultsAreNullOrZero()
    {
        var lr = new LoginResponse();
        Assert.Null(lr.session_key);
        Assert.Equal(0, lr.user_id);
    }

    [Fact]
    public void LoginResponse_PropertiesCanBeSet()
    {
        var lr = new LoginResponse { session_key = "tok123", user_id = 7 };
        Assert.Equal("tok123", lr.session_key);
        Assert.Equal(7, lr.user_id);
    }
}

// ── SavedFile ─────────────────────────────────────────────────────────────────

public class SavedFileTests
{
    [Fact]
    public void SavedFile_DefaultHistoryIsEmpty()
    {
        var sf = new SavedFile();
        Assert.NotNull(sf.History);
        Assert.Empty(sf.History);
    }

    [Fact]
    public void SavedFile_PropertiesRoundTrip()
    {
        var sf = new SavedFile
        {
            ID = 5,
            FileName = "test.jpg",
            Extension = ".jpg",
            Content = new byte[] { 0xFF, 0xD8, 0xFF }
        };

        Assert.Equal(5, sf.ID);
        Assert.Equal("test.jpg", sf.FileName);
        Assert.Equal(".jpg", sf.Extension);
        Assert.Equal(3, sf.Content!.Length);
    }
}

// ── SavedDirectory ────────────────────────────────────────────────────────────

public class SavedDirectoryTests
{
    [Fact]
    public void SavedDirectory_DefaultCollectionsAreEmpty()
    {
        var dir = new SavedDirectory();
        Assert.NotNull(dir.SubDirectories);
        Assert.Empty(dir.SubDirectories);
        Assert.NotNull(dir.Content);
        Assert.Empty(dir.Content);
        Assert.NotNull(dir.History);
        Assert.Empty(dir.History);
    }

    [Fact]
    public void SavedDirectory_CanAddSubDirectories()
    {
        var parent = new SavedDirectory { ID = 1, Name = "Root" };
        var child  = new SavedDirectory { ID = 2, Name = "Child" };
        parent.SubDirectories.Add(child);

        Assert.Single(parent.SubDirectories);
        Assert.Equal("Child", parent.SubDirectories[0].Name);
    }

    [Fact]
    public void SavedDirectory_CanAddFiles()
    {
        var dir = new SavedDirectory { ID = 1, Name = "Docs" };
        dir.Content.Add(new SavedFile { ID = 10, FileName = "a.txt" });
        dir.Content.Add(new SavedFile { ID = 11, FileName = "b.txt" });

        Assert.Equal(2, dir.Content.Count);
    }
}

// ── User (HTTP) ───────────────────────────────────────────────────────────────

public class UserTests : WebserviceTestBase
{
    private readonly User _sut = new();

    [Fact]
    public async Task GetUser_ValidId_ReturnsUser()
    {
        var payload = JsonSerializer.Serialize(new { Id = 3, Email = "alice@example.com", Storage_Plan = 1 });

        MockHttp
            .When(HttpMethod.Get, "http://localhost/user/3")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        User? result = await _sut.GetUser(3);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Id);
        Assert.Equal("alice@example.com", result.Email);
    }

    [Fact]
    public async Task GetUser_NotFound_Throws()
    {
        MockHttp
            .When(HttpMethod.Get, "http://localhost/user/999")
            .Respond(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.GetUser(999));
    }

    [Fact]
    public async Task UpdateLogin_SendsPatchWithEmail()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Patch, "http://localhost/user/updatelogin")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK);

        await _sut.UpdateLogin("bob@example.com");

        Assert.NotNull(capturedBody);
        Assert.Contains("bob@example.com", capturedBody);
    }

    [Fact]
    public async Task UpdateUsedStorage_SendsUserIdAndBytes()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Patch, "http://localhost/user/updateusedstorage")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK);

        await _sut.UpdateUsedStorage(7, 2_000_000L);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);

        // Use case-insensitive property lookup
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = doc.RootElement;

        var userIdProp = root.EnumerateObject().First(p => p.Name.Equals("UserID", StringComparison.OrdinalIgnoreCase));
        var bytesProp = root.EnumerateObject().First(p => p.Name.Equals("usedBytes", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(7, userIdProp.Value.GetInt32());
        Assert.Equal(2_000_000L, bytesProp.Value.GetInt64());
    }

    [Fact]
    public async Task UpdateLogin_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Patch, "http://localhost/user/updatelogin")
            .Respond(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.UpdateLogin("e@e.com"));
    }
}

// ── SavedFile GetHistory (HTTP) ────────────────────────────────────────────────

public class SavedFileGetHistoryTests : WebserviceTestBase
{
    [Fact]
    public async Task GetHistory_ReturnsAndStoresList()
    {
        var items = new[]
        {
            new
            {
                ChangedDate = "2024-05-01",
                ChangedTime = "10:00:00",
                Size        = 512.0,
                ChangedUser = "1",
                Owner       = "2",
                Name        = "img.png"
            }
        };
        string payload = JsonSerializer.Serialize(items);

        MockHttp
            .When(HttpMethod.Get, "http://localhost/history/file/5")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        var file = new SavedFile { ID = 5 };
        List<Info>? result = await file.GetHistory();

        Assert.NotNull(result);
        Assert.Single(result!);
        // History property should be mutated as well
        Assert.Same(result, file.History);
    }
}

// ── SavedDirectory GetHistory (HTTP) ──────────────────────────────────────────

public class SavedDirectoryGetHistoryTests : WebserviceTestBase
{
    [Fact]
    public async Task GetHistory_ReturnsAndStoresList()
    {
        string payload = JsonSerializer.Serialize(Array.Empty<object>());

        MockHttp
            .When(HttpMethod.Get, "http://localhost/history/directory/3")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        var dir = new SavedDirectory { ID = 3 };
        List<Info>? result = await dir.GetHistory();

        Assert.NotNull(result);
        Assert.Empty(result!);
        Assert.Same(result, dir.History);
    }
}
