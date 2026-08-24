using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Stalksville.Application.Models;

namespace Stalksville.Api.Security;

public interface ITokenService
{
    LoginResponse CreateToken(UserDto user);
}

/// <summary>Issues the HS256 JWTs used by the SPA. The signing key never leaves the backend.</summary>
public sealed class JwtTokenService(string signingKey, int expireMinutes) : ITokenService
{
    public LoginResponse CreateToken(UserDto user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(Math.Max(5, expireMinutes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("name", user.Username),
            new("role", user.Role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "stalksville",
            audience: "stalksville-web",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user);
    }
}
