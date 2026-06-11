using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RichardSzalay.MockHttp;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;

public class BackupServiceTests : WebserviceTestBase
{
    private readonly BackupService _sut = new();

    // ── GetBackupFolder ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBackupFolder_ValidId_ReturnsDirectoryList()
    {
        var dirs = new[]
        {
            new { ID = 1, Name = "Backup_2024" },
            new { ID = 2, Name = "Backup_2025" }
        };
        string payload = JsonSerializer.Serialize(dirs);

        MockHttp
            .When(HttpMethod.Get, "http://localhost/backup/directory/5")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        List<SavedDirectory>? result = await _sut.GetBackupFolder(5);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("Backup_2024", result[0].Name);
        Assert.Equal("Backup_2025", result[1].Name);
    }

    [Fact]
    public async Task GetBackupFolder_EmptyJson_ReturnsEmptyList()
    {
        MockHttp
            .When(HttpMethod.Get, "http://localhost/backup/directory/10")
            .Respond(HttpStatusCode.OK, "application/json", "[]");

        List<SavedDirectory>? result = await _sut.GetBackupFolder(10);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task GetBackupFolder_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Get, "http://localhost/backup/directory/99")
            .Respond(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.GetBackupFolder(99));
    }

    // ── GetBackupFile ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBackupFile_ValidId_ReturnsSavedFile()
    {
        var payload = JsonSerializer.Serialize(new { ID = 42, FileName = "notes.txt", Extension = ".txt" });

        MockHttp
            .When(HttpMethod.Get, "http://localhost/backup/file/42")
            .Respond(HttpStatusCode.OK, "application/json", payload);

        SavedFile? result = await _sut.GetBackupFile(42);

        Assert.NotNull(result);
        Assert.Equal(42, result!.ID);
        Assert.Equal("notes.txt", result.FileName);
        Assert.Equal(".txt", result.Extension);
    }

    [Fact]
    public async Task GetBackupFile_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Get, "http://localhost/backup/file/0")
            .Respond(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.GetBackupFile(0));
    }
}
