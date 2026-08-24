using Stalksville.Domain.Entities;

namespace Stalksville.Intelligence.Engine;

public enum MembershipAction
{
    Started = 0,
    Ended = 1
}

public sealed record MembershipTransition(Guid ClanId, MembershipAction Action, DateTimeOffset At);

/// <summary>
/// Derives membership transitions from open (current) memberships and the clan observed in a new
/// observation. Handles: joining a clan, switching clans (end old + start new), and leaving the
/// last clan.
/// </summary>
public static class MembershipTracker
{
    public static IReadOnlyList<MembershipTransition> Track(
        IReadOnlyList<ClanMembership> openMemberships,
        Guid? observedClanId,
        DateTimeOffset at)
    {
        // Defensive: callers pass open memberships, but ended ones must never produce transitions.
        openMemberships = [.. openMemberships.Where(m => m.EndedAt is null)];

        var transitions = new List<MembershipTransition>();

        if (observedClanId is null)
        {
            foreach (var membership in openMemberships)
            {
                transitions.Add(new MembershipTransition(membership.ClanId, MembershipAction.Ended, at));
            }

            return transitions;
        }

        var staysInObservedClan = false;

        foreach (var membership in openMemberships)
        {
            if (membership.ClanId == observedClanId)
            {
                staysInObservedClan = true;
            }
            else
            {
                transitions.Add(new MembershipTransition(membership.ClanId, MembershipAction.Ended, at));
            }
        }

        if (!staysInObservedClan)
        {
            transitions.Add(new MembershipTransition(observedClanId.Value, MembershipAction.Started, at));
        }

        return transitions;
    }
}
