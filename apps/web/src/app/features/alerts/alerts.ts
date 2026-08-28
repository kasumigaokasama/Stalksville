import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RouterLink } from '@angular/router';

import { AlertDto } from '../../core/api/api.model';
import { UnreadAlerts } from '../../core/alerts/unread-alerts';
import { formatDateTime, formatRelative } from '../../shared/util/format';

/** In-app inbox over derived intelligence alerts — every row links back to its evidence. */
@Component({
  selector: 'stl-alerts',
  imports: [RouterLink],
  styleUrl: './alerts.scss',
  templateUrl: './alerts.html',
})
export class Alerts {
  private readonly http = inject(HttpClient);
  private readonly unreadAlerts = inject(UnreadAlerts);

  protected readonly showUnreadOnly = signal(false);
  protected readonly kindFilter = signal('all');
  protected readonly busy = signal(false);

  private readonly alertsResource = httpResource<AlertDto[]>(() => {
    const params = new URLSearchParams();
    if (this.showUnreadOnly()) {
      params.set('unreadOnly', 'true');
    }
    if (this.kindFilter() !== 'all') {
      params.set('kind', this.kindFilter());
    }
    return `/api/v1/alerts?${params.toString()}`;
  });

  protected readonly alerts = computed(() => (this.alertsResource.hasValue() ? this.alertsResource.value() ?? [] : []));
  protected readonly isLoading = computed(() => this.alertsResource.isLoading());
  protected readonly kinds = computed(() => [...new Set(this.alerts().map((a) => a.kind))].sort());

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;

  protected async markRead(alert: AlertDto): Promise<void> {
    if (alert.readAt) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post(`/api/v1/alerts/${alert.id}/read`, null));
      this.alertsResource.reload();
      void this.unreadAlerts.refresh();
    } finally {
      this.busy.set(false);
    }
  }

  protected async markAllRead(): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post('/api/v1/alerts/read-all', null));
      this.alertsResource.reload();
      void this.unreadAlerts.refresh();
    } finally {
      this.busy.set(false);
    }
  }
}
