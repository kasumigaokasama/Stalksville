using Stalksville.Domain.Models;

namespace Stalksville.Application.Models;

/// <summary>A single observation fetched from Wolvesville: raw payload, normalized state, provenance.</summary>
public sealed record PlayerObservation(
    string RawJson,
    NormalizedPlayerState State,
    string Source);

/// <summary>Clan data as observed from Wolvesville (search results or clan info).</summary>
public sealed record ObservedClan(
    string WolvesvilleClanId,
    string Name,
    string? Description,
    int? MemberCount,
    string? LanguageCode,
    string? JoinType,
    long? Xps,
    int? Level,
    string? LeaderWolvesvillePlayerId,
    IReadOnlyList<string> MemberWolvesvillePlayerIds,
    string Source);
