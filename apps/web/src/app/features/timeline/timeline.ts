import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { TimelineEventDto } from '../../core/api/api.model';
import { formatDateTime, formatRelative } from '../../shared/util/format';
import { Skeleton } from '../../shared/ui/skeleton';

/** Global timeline of every observation and derivation, with filters and load-more paging. */
@Component({
  selector: 'stl-timeline',
  imports: [RouterLink, Skeleton],
  styleUrl: './timeline.scss',
  templateUrl: './timeline.html',
})
export class Timeline {
  private readonly http = inject(HttpClient);

  private static readonly pageSize = 100;

  protected readonly entityFilter = signal<'all' | 'player' | 'clan'>('all');
  protected readonly kindFilter = signal('all');
  protected readonly typeFilter = signal('all');

  /** Event types the API records (Domain TimelineEventTypes) — a stable filter list. */
  protected readonly eventTypes = [
    'PlayerDiscovered',
    'PlayerReobserved',
    'ClanChanged',
    'LevelChanged',
    'ProfileChanged',
    'CosmeticsChanged',
    'RankStateChanged',
    'ClanImported',
    'MembershipStarted',
    'MembershipEnded',
    'HighscoreRankChanged',
    'RankedRankChanged',
    'FriendshipChanged',
    'ExposureShifted',
  ] as const;

  protected readonly rows = signal<TimelineEventDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly initialized = signal(false);
  protected readonly hasMore = signal(false);
  private offset = 0;

  constructor() {
    void this.load(true);
  }

  protected reload(): void {
    void this.load(true);
  }

  protected loadMore(): void {
    void this.load(false);
  }

  private async load(reset: boolean): Promise<void> {
    if (this.loading()) {
      return;
    }
    this.loading.set(true);
    const offset = reset ? 0 : this.offset;
    try {
      const params = new URLSearchParams({ limit: String(Timeline.pageSize), offset: String(offset) });
      if (this.entityFilter() !== 'all') {
        params.set('entity', this.entityFilter());
      }
      if (this.kindFilter() === 'observed') {
        params.set('derived', 'false');
      } else if (this.kindFilter() === 'derived') {
        params.set('derived', 'true');
      }
      if (this.typeFilter() !== 'all') {
        params.set('eventType', this.typeFilter());
      }
      const page = await firstValueFrom(
        this.http.get<TimelineEventDto[]>(`/api/v1/timeline?${params.toString()}`),
      );
      this.rows.set(reset ? page : [...this.rows(), ...page]);
      this.offset = offset + page.length;
      this.hasMore.set(page.length === Timeline.pageSize);
    } finally {
      this.initialized.set(true);
      this.loading.set(false);
    }
  }

  protected entityLink(event: TimelineEventDto): string {
    return event.entityType === 'player' ? `/players/${event.entityId}` : `/clans/${event.entityId}`;
  }

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;
}
