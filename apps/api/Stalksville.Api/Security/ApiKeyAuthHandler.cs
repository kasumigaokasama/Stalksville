using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Security;

/// <summary>Options for the X-Api-Key authentication scheme.</summary>
public sealed class ApiKeyOptions : AuthenticationSchemeOptions;

/// <summary>
/// Authenticates requests carrying X-Api-Key. The key is looked up by SHA-256 hash (never stored
/// in clear); the resulting principal carries the same short claims (sub/name/role) the JWT path
/// issues, so policies and rate limiting work identically for both credential kinds.
/// </summary>
public sealed class ApiKeyAuthHandler(
    IOptionsMonitor<ApiKeyOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKeys = Context.RequestServices.GetRequiredService<IApiKeyStore>();

        var match = await apiKeys.FindActiveByHashAsync(Hash(provided.ToString()));
        if (match is null)
        {
            return AuthenticateResult.Fail("Unknown or revoked API key.");
        }

        var (key, user) = match.Value;
        await apiKeys.TouchAsync(key.Id, DateTimeOffset.UtcNow);

        var identity = new ClaimsIdentity(
        [
            new Claim("sub", user.Id.ToString()),
            new Claim("name", user.Username),
            new Claim("role", user.Role.ToString().ToUpperInvariant())
        ],
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    public static string Hash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Generates a display-friendly key: stv_&lt;43 url-safe chars&gt;. Show once.</summary>
    public static (string Key, string Prefix) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var key = "stv_" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (key, key[..12]);
    }
}
