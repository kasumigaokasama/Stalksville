using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;

namespace Stalksville.Intelligence.Engine;

public sealed record ExposureFactor(string Label, int Points, string Evidence);

public sealed record ExposureCategory(string Name, int Score, IReadOnlyList<ExposureFactor> Factors);

public sealed record ExposureResult(
    int Overall,
    IReadOnlyList<ExposureCategory> Categories)
{
    public const string ConfidenceNote = "Deterministic scoring over observed data; every point lists its evidence.";
}

/// <summary>
/// Explainable public-information exposure score (master plan §15). Measures how much surface a
/// player's public Wolvesville data exposes — it never makes claims about real-world identity.
/// Pure function of observed/derived dossier inputs; the "why" is part of the result.
/// </summary>
public static class ExposureAnalyzer
{
    public static ExposureResult Analyze(
        NormalizedPlayerState state,
        IReadOnlyList<ClanMembership> memberships,
        IReadOnlyList<Relationship> relationships,
        int? currentClanMemberCount)
    {
        var identity = AnalyzeIdentity(state);
        var clan = AnalyzeClan(state, currentClanMemberCount);
        var historical = AnalyzeHistorical(memberships);
        var network = AnalyzeNetwork(relationships);
        var profile = AnalyzeProfile(state);

        var categories = new[] { identity, clan, historical, network, profile };
        var overall = (int)Math.Round(categories.Average(c => c.Score));

        return new ExposureResult(overall, categories);
    }

    private static ExposureCategory AnalyzeIdentity(NormalizedPlayerState state)
    {
        var factors = new List<ExposureFactor>
        {
            new("Public profile exists", 20, "player profile is publicly readable via the Wolvesville API")
        };

        if (!string.IsNullOrEmpty(state.PersonalMessage))
        {
            factors.Add(new ExposureFactor("Personal message is set", 15, "personalMessage present in profile"));
        }

        if (state.FriendCount > 0)
        {
            factors.Add(new ExposureFactor($"Friend list exposes {state.FriendCount} connections", Math.Min(20, state.FriendCount * 2), "friendIds in profile"));
        }

        if (state.Status is not null)
        {
            factors.Add(new ExposureFactor("Online status is public", 5, "status field in profile"));
        }

        return new ExposureCategory("Identity surface", Cap(factors), factors);
    }

    private static ExposureCategory AnalyzeClan(NormalizedPlayerState state, int? memberCount)
    {
        var factors = new List<ExposureFactor>();

        if (state.ClanWolvesvilleId is not null)
        {
            factors.Add(new ExposureFactor("Currently in a public clan", 30, $"clanId {state.ClanWolvesvilleId} in profile"));
            if (memberCount is > 0)
            {
                factors.Add(new ExposureFactor($"Clan lists {memberCount} members", Math.Min(50, memberCount.Value), "clan member list"));
            }
        }
        else
        {
            factors.Add(new ExposureFactor("No current clan", 0, "clanId absent in profile"));
        }

        return new ExposureCategory("Clan exposure", Cap(factors), factors);
    }

    private static ExposureCategory AnalyzeHistorical(IReadOnlyList<ClanMembership> memberships)
    {
        var ended = memberships.Count(m => m.EndedAt is not null);
        var factors = new List<ExposureFactor>
        {
            new(ended == 0 ? "No historical memberships recorded" : $"{ended} ended membership(s) recorded", Math.Min(80, ended * 25),
                ended == 0 ? "no ended clan_memberships rows" : "ended clan_memberships rows")
        };

        if (memberships.Count > 1)
        {
            factors.Add(new ExposureFactor("Multiple membership periods visible", 20, "clan_memberships history"));
        }

        return new ExposureCategory("Historical exposure", Cap(factors), factors);
    }

    private static ExposureCategory AnalyzeNetwork(IReadOnlyList<Relationship> relationships)
    {
        var current = relationships.Count(r => r.IsCurrent);
        var total = relationships.Count;
        var factors = new List<ExposureFactor>
        {
            new($"{current} current relationship(s)", Math.Min(60, current * 20), "relationships table"),
            new($"{total} relationship(s) total", Math.Min(40, total * 10), "relationships table")
        };

        return new ExposureCategory("Network exposure", Cap(factors), factors);
    }

    private static ExposureCategory AnalyzeProfile(NormalizedPlayerState state)
    {
        var factors = new List<ExposureFactor>();

        if (state.BadgeIds.Count > 0)
        {
            factors.Add(new ExposureFactor($"{state.BadgeIds.Count} public badges", Math.Min(40, state.BadgeIds.Count * 3), "badgeIds in profile"));
        }

        if (state.RoleCardIds.Count > 0)
        {
            factors.Add(new ExposureFactor($"{state.RoleCardIds.Count} role card(s)", Math.Min(30, state.RoleCardIds.Count * 5), "roleCards in profile"));
        }

        if (state.EquippedAvatarId is not null)
        {
            factors.Add(new ExposureFactor("Equipped avatar visible", 10, "equippedAvatar in profile"));
        }

        if (factors.Count == 0)
        {
            factors.Add(new ExposureFactor("Minimal public cosmetics", 0, "no badges/avatars in profile"));
        }

        return new ExposureCategory("Profile exposure", Cap(factors), factors);
    }

    private static int Cap(List<ExposureFactor> factors) => Math.Clamp(factors.Sum(f => f.Points), 0, 100);
}
