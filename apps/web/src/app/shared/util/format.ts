/** Formatting helpers (template pipes are avoided — components format via these). */

const dateFmt = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
});

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) {
    return '—';
  }
  return dateFmt.format(new Date(iso));
}

export function formatRelative(iso: string | null | undefined): string {
  if (!iso) {
    return '—';
  }
  const ms = Date.now() - new Date(iso).getTime();
  const minutes = Math.round(ms / 60_000);
  if (minutes < 1) {
    return 'just now';
  }
  if (minutes < 60) {
    return `${minutes}m ago`;
  }
  const hours = Math.round(minutes / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }
  const days = Math.round(hours / 24);
  if (days < 30) {
    return `${days}d ago`;
  }
  return formatDateTime(iso);
}

export function formatNumber(value: number | null | undefined): string {
  return value == null ? '—' : new Intl.NumberFormat().format(value);
}

/** Human label for machine field names used in change records. */
export function fieldLabel(field: string): string {
  const labels: Record<string, string> = {
    username: 'Username',
    personalMessage: 'Personal message',
    level: 'Level',
    status: 'Status',
    clanId: 'Clan',
    wins: 'Wins',
    losses: 'Losses',
    gamesPlayed: 'Games played',
    receivedRosesCount: 'Roses received',
    sentRosesCount: 'Roses sent',
    profileIconId: 'Profile icon',
    equippedAvatarId: 'Equipped avatar',
    achievements: 'Achievements',
    rankedSeason: 'Ranked season',
    rankedWins: 'Ranked wins',
    rankedLosses: 'Ranked losses',
    rankedCurrentRating: 'Ranked rating',
    badgeIds: 'Badges',
    roleCardIds: 'Role cards',
  };
  return labels[field] ?? field;
}
