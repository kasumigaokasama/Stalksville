namespace Stalksville.Domain.Entities;

public enum UserRole
{
    Admin = 0,
    Analyst = 1,
    Viewer = 2
}

public sealed class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Viewer;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
