using System.Globalization;
using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;

namespace Stalksville.Intelligence.Engine;

/// <summary>A single field-level difference between two normalized states.</summary>
public sealed record FieldChange(string Field, PlayerChangeKind Kind, string? OldValue, string? NewValue);

/// <summary>
/// Field-level diff of two normalized player states. Volatile fields (lastOnline) are excluded —
/// they change constantly and carry no intelligence value.
/// </summary>
public static class ChangeDetector
{
    public static IReadOnlyList<FieldChange> Detect(NormalizedPlayerState previous, NormalizedPlayerState current)
    {
        var changes = new List<FieldChange>();

        Scalar(changes, "username", previous.Username, current.Username);
        Scalar(changes, "personalMessage", previous.PersonalMessage, current.PersonalMessage);
        Scalar(changes, "level", previous.Level, current.Level);
        Scalar(changes, "status", previous.Status, current.Status);
        Scalar(changes, "clanId", previous.ClanWolvesvilleId, current.ClanWolvesvilleId);
        Scalar(changes, "wins", previous.Wins, current.Wins);
        Scalar(changes, "losses", previous.Losses, current.Losses);
        Scalar(changes, "gamesPlayed", previous.GamesPlayed, current.GamesPlayed);
        Scalar(changes, "receivedRosesCount", previous.ReceivedRosesCount, current.ReceivedRosesCount);
        Scalar(changes, "sentRosesCount", previous.SentRosesCount, current.SentRosesCount);
        Scalar(changes, "profileIconId", previous.ProfileIconId, current.ProfileIconId);
        Scalar(changes, "equippedAvatarId", previous.EquippedAvatarId, current.EquippedAvatarId);
        Scalar(changes, "achievements", previous.Achievements, current.Achievements);
        Scalar(changes, "rankedSeason", previous.RankedSeason, current.RankedSeason);
        Scalar(changes, "rankedWins", previous.RankedWins, current.RankedWins);
        Scalar(changes, "rankedLosses", previous.RankedLosses, current.RankedLosses);
        Scalar(changes, "rankedCurrentRating", previous.RankedCurrentRating, current.RankedCurrentRating);

        Set(changes, "badgeIds", previous.BadgeIds, current.BadgeIds);
        Set(changes, "roleCardIds", previous.RoleCardIds, current.RoleCardIds);
        Set(changes, "friendIds", previous.FriendWolvesvilleIds, current.FriendWolvesvilleIds);

        return changes;
    }

    /// <summary>Maps a set of field changes to coarse timeline classifications.</summary>
    public static IReadOnlyList<string> Classify(IReadOnlyList<FieldChange> changes)
    {
        var fields = changes.Select(c => c.Field).ToHashSet(StringComparer.Ordinal);
        var classifications = new List<string>();

        if (fields.Contains("clanId"))
        {
            classifications.Add(TimelineEventTypes.ClanChanged);
        }

        if (fields.Contains("level"))
        {
            classifications.Add(TimelineEventTypes.LevelChanged);
        }

        if (fields.Contains("personalMessage") || fields.Contains("username") || fields.Contains("status"))
        {
            classifications.Add(TimelineEventTypes.ProfileChanged);
        }

        if (fields.Contains("badgeIds") || fields.Contains("equippedAvatarId") || fields.Contains("profileIconId") || fields.Contains("roleCardIds"))
        {
            classifications.Add(TimelineEventTypes.CosmeticsChanged);
        }

        if (fields.Contains("rankedSeason") || fields.Contains("rankedWins") || fields.Contains("rankedLosses") || fields.Contains("rankedCurrentRating"))
        {
            classifications.Add(TimelineEventTypes.RankStateChanged);
        }

        if (fields.Contains("friendIds"))
        {
            classifications.Add(TimelineEventTypes.FriendshipChanged);
        }

        return classifications;
    }

    private static void Scalar(List<FieldChange> changes, string field, string? previous, string? current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange(field, PlayerChangeKind.Scalar, previous, current));
        }
    }

    private static void Scalar(List<FieldChange> changes, string field, int? previous, int? current)
    {
        if (previous != current)
        {
            changes.Add(new FieldChange(field, PlayerChangeKind.Scalar, previous?.ToString(CultureInfo.InvariantCulture), current?.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void Scalar(List<FieldChange> changes, string field, int previous, int current)
    {
        if (previous != current)
        {
            changes.Add(new FieldChange(field, PlayerChangeKind.Scalar, previous.ToString(CultureInfo.InvariantCulture), current.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void Set(List<FieldChange> changes, string field, IReadOnlyList<string> previous, IReadOnlyList<string> current)
    {
        var previousSet = previous.ToHashSet(StringComparer.Ordinal);
        var currentSet = current.ToHashSet(StringComparer.Ordinal);

        foreach (var added in currentSet.Except(previousSet).OrderBy(id => id, StringComparer.Ordinal))
        {
            changes.Add(new FieldChange(field, PlayerChangeKind.SetAddition, null, added));
        }

        foreach (var removed in previousSet.Except(currentSet).OrderBy(id => id, StringComparer.Ordinal))
        {
            changes.Add(new FieldChange(field, PlayerChangeKind.SetRemoval, removed, null));
        }
    }
}
