using Npgsql;

namespace Stalksville.IntegrationTests;

/// <summary>Creates and drops throwaway PostgreSQL databases for integration test classes.</summary>
public static class TestDatabase
{
    private const string AdminConnectionString = "Host=localhost;Port=5432;Username=stalksville;Password=stalksville;Database=postgres";

    public static async Task<string> CreateAsync()
    {
        var name = $"stalksville_test_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{name}\"";
        await command.ExecuteNonQueryAsync();

        return $"Host=localhost;Port=5432;Database={name};Username=stalksville;Password=stalksville";
    }

    public static async Task DropAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var name = builder.Database;

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
