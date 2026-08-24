using Stalksville.Application.Abstractions;

namespace Stalksville.Infrastructure.Security;

/// <summary>Standard ASP.NET Core Identity PBKDF2 hashing behind the application port.</summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> _inner = new();

    public string Hash(string password) => _inner.HashPassword(new object(), password);

    public bool Check(string passwordHash, string password)
        => _inner.VerifyHashedPassword(new object(), passwordHash, password)
            == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success;
}
