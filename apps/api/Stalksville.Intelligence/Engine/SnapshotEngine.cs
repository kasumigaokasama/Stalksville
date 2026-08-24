using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stalksville.Domain.Models;

namespace Stalksville.Intelligence.Engine;

/// <summary>
/// Canonical serialization + hashing of normalized player states. A snapshot's identity is the
/// hash of its canonical form (with volatile fields excluded), which is what makes smart
/// snapshotting (skip identical states) and evidence payload hashes possible.
/// </summary>
public static class SnapshotEngine
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>Canonical JSON of the full observed state — this is what gets stored as the payload.</summary>
    public static string Canonicalize(NormalizedPlayerState state)
    {
        return JsonSerializer.Serialize(state, CanonicalJson);
    }

    public static NormalizedPlayerState? Parse(string canonicalPayload)
    {
        try
        {
            return JsonSerializer.Deserialize<NormalizedPlayerState>(canonicalPayload, CanonicalJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Hash over the canonical form with volatile fields cleared (currently lastOnline), so a
    /// player who merely came online does not produce a new snapshot.
    /// </summary>
    public static string ComputeHash(NormalizedPlayerState state)
    {
        var withoutVolatile = state with { LastOnline = null };
        return HashString(JsonSerializer.Serialize(withoutVolatile, CanonicalJson));
    }

    public static string HashString(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
