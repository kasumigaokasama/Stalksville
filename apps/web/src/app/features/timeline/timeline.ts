import { httpResource } from '@angular/common/http';
import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TimelineEventDto } from '../../core/api/api.model';
import { formatDateTime, formatRelative } from '../../shared/util/format';

/** Global timeline of every observation and derivation, with filters (master plan §17). */
@Component({
  selector: 'stl-timeline',
  imports: [RouterLink],
  styleUrl: './timeline.scss',
  templateUrl: './timeline.html',
})
export class Timeline {
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
  ] as const;

  private readonly events = httpResource<TimelineEventDto[]>(() => {
    const params = new URLSearchParams();
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
    params.set('limit', '200');
    return `/api/v1/timeline?${params.toString()}`;
  });

  protected readonly timeline = computed(() => (this.events.hasValue() ? this.events.value() ?? [] : []));
  protected readonly isLoading = computed(() => this.events.isLoading());

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;

  protected entityLink(event: TimelineEventDto): string {
    return event.entityType === 'player' ? `/players/${event.entityId}` : `/clans/${event.entityId}`;
  }
}
