import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RouterLink } from '@angular/router';

import { AlertDto } from '../../core/api/api.model';
import { UnreadAlerts } from '../../core/alerts/unread-alerts';
import { formatDateTime, formatRelative } from '../../shared/util/format';
import { Skeleton } from '../../shared/ui/skeleton';

/** In-app inbox over derived intelligence alerts — every row links back to its evidence. */
@Component({
  selector: 'stl-alerts',
  imports: [RouterLink, Skeleton],
  styleUrl: './alerts.scss',
  templateUrl: './alerts.html',
})
export class Alerts {
  private readonly http = inject(HttpClient);
  private readonly unreadAlerts = inject(UnreadAlerts);

  private static readonly pageSize = 50;

  protected readonly showUnreadOnly = signal(false);
  protected readonly kindFilter = signal('all');
  protected readonly busy = signal(false);
  protected readonly rows = signal<AlertDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly initialized = signal(false);
  protected readonly hasMore = signal(false);
  private offset = 0;

  constructor() {
    void this.load(true);
  }

  protected readonly isLoading = computed(() => this.loading());
  protected readonly alerts = computed(() => this.rows());
  protected readonly kinds = computed(() => [...new Set(this.rows().map((a) => a.kind))].sort());

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
      const params = new URLSearchParams({ limit: String(Alerts.pageSize), offset: String(offset) });
      if (this.showUnreadOnly()) {
        params.set('unreadOnly', 'true');
      }
      if (this.kindFilter() !== 'all') {
        params.set('kind', this.kindFilter());
      }
      const page = await firstValueFrom(this.http.get<AlertDto[]>(`/api/v1/alerts?${params.toString()}`));
      this.rows.set(reset ? page : [...this.rows(), ...page]);
      this.offset = offset + page.length;
      this.hasMore.set(page.length === Alerts.pageSize);
    } finally {
      this.initialized.set(true);
      this.loading.set(false);
    }
  }

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;

  /** Route by entity type — alerts are not always about players. */
  protected entityLink(alert: AlertDto): string {
    switch (alert.entityType) {
      case 'player':
        return `/players/${alert.entityId}`;
      case 'clan':
        return `/clans/${alert.entityId}`;
      case 'investigation':
        return `/investigations/${alert.entityId}`;
      default:
        return '/dashboard';
    }
  }

  protected async markRead(alert: AlertDto): Promise<void> {
    if (alert.readAt) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post(`/api/v1/alerts/${alert.id}/read`, null));
      this.reload();
      void this.unreadAlerts.refresh();
    } finally {
      this.busy.set(false);
    }
  }

  protected async markAllRead(): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post('/api/v1/alerts/read-all', null));
      this.reload();
      void this.unreadAlerts.refresh();
    } finally {
      this.busy.set(false);
    }
  }
}
