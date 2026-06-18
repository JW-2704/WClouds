using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using RichardSzalay.MockHttp;
using Xunit;
using WClouds_WPF.Logic;

namespace WClouds_WPF.Tests;
// ALL tests were made with ai, prompt: can you make xunit tests for this project as much as possible
public class AuthenticatorTests : WebserviceTestBase
{
    private readonly Authenticator _sut = new();

    // AI Agent: Register() generiert jetzt lokal ein RSA-Keypair, sendet
    // den Public Key mit und parst die Server-Antwort nach "id", um den
    // temporaeren Key auf die echte userId umzubenennen - die Mock-Antwort
    // muss daher ein echtes JSON-Objekt mit "id" sein statt eines blossen
    // Strings.
    private static readonly string RegisterOkPayload =
        JsonSerializer.Serialize(new { id = 12345, email = "test@example.com", storage_plan = 100 });

    // ── Register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_SuccessfulResponse_ReturnsBody()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/register")
            .Respond(HttpStatusCode.OK, "application/json", RegisterOkPayload);

        string result = await _sut.Register("test@example.com", "password123", "my-storage-key");

        Assert.Equal(RegisterOkPayload, result);
    }

    [Fact]
    public async Task Register_HashesPassword_NotSentAsPlaintext()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/register")
            .With(req =>
            {
                capturedBody = req.Content!.ReadAsStringAsync().Result;
                return true;
            })
            .Respond(HttpStatusCode.OK, "application/json", RegisterOkPayload);

        await _sut.Register("u@u.com", "hunter2", "key");

        Assert.NotNull(capturedBody);
        // Password must NOT appear in plaintext
        Assert.DoesNotContain("hunter2", capturedBody);
        // Should contain a SHA-256 hex string (64 lowercase hex chars)
        using var doc = JsonDocument.Parse(capturedBody!);
        string password = doc.RootElement.GetProperty("password").GetString()!;
        Assert.Equal(64, password.Length);
        Assert.Matches("^[0-9a-f]{64}$", password);
    }

    [Fact]
    public async Task Register_EncodesStorageKey_AsBase64()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/register")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK, "application/json", RegisterOkPayload);

        await _sut.Register("u@u.com", "pass", "raw-storage-key");

        using var doc = JsonDocument.Parse(capturedBody!);
        string encodedKey = doc.RootElement.GetProperty("storage_plan_key").GetString()!;
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedKey));
        Assert.Equal("raw-storage-key", decoded);
    }

    [Fact]
    public async Task Register_SendsPublicKey()
    {
        string? capturedBody = null;

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/register")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK, "application/json", RegisterOkPayload);

        await _sut.Register("u@u.com", "pass", "key");

        using var doc = JsonDocument.Parse(capturedBody!);
        string publicKey = doc.RootElement.GetProperty("public_key").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(publicKey));
        // Muss als gueltiges Base64 einer RSA-SubjectPublicKeyInfo decodierbar sein
        Assert.True(Convert.FromBase64String(publicKey).Length > 100);
    }

    [Fact]
    public async Task Register_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/register")
            .Respond(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.Register("bad@bad.com", "pass", "key"));
    }

    // ── Login ────────────────────────────────────────────────────────────────

    // AI Agent: Login() ruft jetzt EncryptionService.Initialize(userId) auf,
    // das ohne lokal vorhandenen Private Key bewusst wirft (siehe
    // EncryptionService-Kommentar zu Multi-Device). Tests muessen daher
    // vorher einen Keypair fuer die jeweilige Test-userId anlegen.
    private static void EnsureLocalKeypairFor(int userId)
    {
        string tempId = $"login-test-{Guid.NewGuid()}";
        EncryptionService.GenerateAndStoreNewKeypair(tempId);
        EncryptionService.PersistKeypairForUser(tempId, userId);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsLoginResponse()
    {
        EnsureLocalKeypairFor(42);
        var responsePayload = JsonSerializer.Serialize(new { session_key = "abc123", user_id = 42 });

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/login")
            .Respond(HttpStatusCode.OK, "application/json", responsePayload);

        LoginResponse result = await _sut.Login("user@example.com", "correct-password");

        Assert.Equal("abc123", result.session_key);
        Assert.Equal(42, result.user_id);
    }

    [Fact]
    public async Task Login_SetsApiKeyOnWebservice()
    {
        EnsureLocalKeypairFor(1);
        var responsePayload = JsonSerializer.Serialize(new { session_key = "session-xyz", user_id = 1 });

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/login")
            .Respond(HttpStatusCode.OK, "application/json", responsePayload);

        await _sut.Login("u@u.com", "pass");

        Assert.Equal("session-xyz", Webservice.APIKey);
    }

    [Fact]
    public async Task Login_HashesPassword()
    {
        EnsureLocalKeypairFor(1);
        string? capturedBody = null;
        var responsePayload = JsonSerializer.Serialize(new { session_key = "key", user_id = 1 });

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/login")
            .With(req => { capturedBody = req.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK, "application/json", responsePayload);

        await _sut.Login("u@u.com", "plaintext");

        using var doc = JsonDocument.Parse(capturedBody!);
        string sentPw = doc.RootElement.GetProperty("password").GetString()!;
        Assert.DoesNotContain("plaintext", sentPw);
        Assert.Equal(64, sentPw.Length);
    }

    [Fact]
    public async Task Login_ServerError_Throws()
    {
        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/login")
            .Respond(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.Login("u@u.com", "wrong"));
    }

    [Fact]
    public async Task Login_NoLocalKeypair_ThrowsInvalidOperationException()
    {
        // AI Agent: simuliert Login auf einem neuen Geraet fuer einen
        // Account, der hier noch nie registriert wurde - muss klar
        // scheitern statt einen neuen (inkompatiblen) Keypair zu erzeugen.
        int neverRegisteredUserId = -Math.Abs(Guid.NewGuid().GetHashCode());
        var responsePayload = JsonSerializer.Serialize(new { session_key = "x", user_id = neverRegisteredUserId });

        MockHttp
            .When(HttpMethod.Post, "http://localhost/user/login")
            .Respond(HttpStatusCode.OK, "application/json", responsePayload);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Login("u@u.com", "pass"));
    }
}
