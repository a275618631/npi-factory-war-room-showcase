using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

public class OpenIddictAuthTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var connectionString = _dbContainer.GetConnectionString();
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = connectionString,
                        ["App:SelfUrl"] = "http://localhost:5000",
                        ["AuthServer:Authority"] = "http://localhost:5000",
                        ["AuthServer:RequireHttpsMetadata"] = "false",
                        ["DEMO_USER_PASSWORD"] = "unit-test-only"
                    });
                });
            });
    }

    [Fact]
    [Trait("Category", "")]
    public async Task OpenIdDiscovery_Returns200_WithEndpointsAndS256()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("authorization_endpoint", json);
        Assert.Contains("token_endpoint", json);
        Assert.Contains("authorization_code", json);
        Assert.Contains("S256", json);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task AuthorizeEndpoint_Unauthenticated_RedirectsToLogin()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authorizeUrl = "/connect/authorize?client_id=npi-web&response_type=code&redirect_uri=http%3A%2F%2Flocalhost%3A5173%2Fcallback&scope=openid%20profile%20email&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256";
        var response = await client.GetAsync(authorizeUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Account/Login", response.Headers.Location.OriginalString);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task LoginPage_InvalidCredentials_Fails_CannotGetToken()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var returnUrl = "/connect/authorize?client_id=npi-web&response_type=code&redirect_uri=http%3A%2F%2Flocalhost%3A5173%2Fcallback&scope=openid%20profile%20email&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256";
        var getResp = await client.GetAsync($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var html = await getResp.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Verification token should exist on login page");
        var antiForgeryToken = tokenMatch.Groups[1].Value;

        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginInput.UserNameOrEmailAddress"] = "demo",
            ["LoginInput.Password"] = "WrongPassword!",
            ["Action"] = "Login",
            ["__RequestVerificationToken"] = antiForgeryToken
        });

        var postResp = await client.PostAsync($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}", postContent);
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        var postHtml = await postResp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid username or password", postHtml);

        // Attempting to exchange invalid/forged authorization code fails
        var tokenPostContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "npi-web",
            ["redirect_uri"] = "http://localhost:5173/callback",
            ["code"] = "invalid_code",
            ["code_verifier"] = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        });
        var tokenResp = await client.PostAsync("/connect/token", tokenPostContent);
        Assert.Equal(HttpStatusCode.BadRequest, tokenResp.StatusCode);
    }

    [Fact]
    [Trait("Category", "")]
    public async Task AuthorizationCodeWithPkce_DemoCredentials_ReturnsAccessToken()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        const string codeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string codeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
        const string redirectUri = "http://localhost:5173/callback";

        var authorizeUrl = $"/connect/authorize?client_id=npi-web&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=openid%20profile%20email&code_challenge={codeChallenge}&code_challenge_method=S256";

        // 1. Visit Login page to retrieve CSRF token
        var getResp = await client.GetAsync($"/Account/Login?ReturnUrl={Uri.EscapeDataString(authorizeUrl)}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var html = await getResp.Content.ReadAsStringAsync();

        var tokenMatch = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Verification token should exist on login page");
        var antiForgeryToken = tokenMatch.Groups[1].Value;

        // 2. Submit demo credentials
        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginInput.UserNameOrEmailAddress"] = "demo",
            ["LoginInput.Password"] = "unit-test-only",
            ["Action"] = "Login",
            ["__RequestVerificationToken"] = antiForgeryToken
        });

        var postResp = await client.PostAsync($"/Account/Login?ReturnUrl={Uri.EscapeDataString(authorizeUrl)}", postContent);
        Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);
        Assert.NotNull(postResp.Headers.Location);

        // 3. Follow redirect back to /connect/authorize
        var authorizeResp = await client.GetAsync(postResp.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, authorizeResp.StatusCode);
        Assert.NotNull(authorizeResp.Headers.Location);

        // 4. Extract authorization code from callback redirect URI
        var location = authorizeResp.Headers.Location.OriginalString;
        Assert.StartsWith(redirectUri, location);
        var codeMatch = Regex.Match(location, @"[?&]code=([^&]+)");
        Assert.True(codeMatch.Success, "Authorization code should be returned in callback URL");
        var authCode = Uri.UnescapeDataString(codeMatch.Groups[1].Value);

        // 5. Exchange code + PKCE verifier for access token
        var tokenPostContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "npi-web",
            ["redirect_uri"] = redirectUri,
            ["code"] = authCode,
            ["code_verifier"] = codeVerifier
        });

        var tokenResp = await client.PostAsync("/connect/token", tokenPostContent);
        Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);

        var tokenJson = await tokenResp.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(tokenJson);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("access_token", out var accessTokenElem));
        Assert.False(string.IsNullOrWhiteSpace(accessTokenElem.GetString()));

        Assert.True(root.TryGetProperty("token_type", out var tokenTypeElem));
        Assert.Equal("Bearer", tokenTypeElem.GetString());
    }
}




