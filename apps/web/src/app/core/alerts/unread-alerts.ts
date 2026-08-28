import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { firstValueFrom, filter, Subscription } from 'rxjs';

import { UnreadCountDto } from '../api/api.model';
import { AuthService } from '../auth/auth';

/**
 * Unread alert count for the shell badge. Polls on navigation and every 60s while signed in;
 * the inbox page calls refresh() after marking alerts read so the badge stays honest.
 */
@Injectable({ providedIn: 'root' })
export class UnreadAlerts {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly pollingInterval = 60_000;
  private timer: ReturnType<typeof setInterval> | null = null;
  private navSub: Subscription;

  readonly unread = signal(0);

  constructor() {
    effect(() => {
      if (this.auth.user()) {
        this.start();
      } else {
        this.stop();
      }
    });

    this.navSub = this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => {
      if (this.auth.user()) {
        void this.refresh();
      }
    });
  }

  async refresh(): Promise<void> {
    try {
      const result = await firstValueFrom(this.http.get<UnreadCountDto>('/api/v1/alerts/unread-count'));
      this.unread.set(result.unread);
    } catch {
      // The badge is best-effort; failures surface on the alerts page itself.
    }
  }

  private start(): void {
    if (this.timer !== null) {
      return;
    }
    void this.refresh();
    this.timer = setInterval(() => void this.refresh(), this.pollingInterval);
  }

  private stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.unread.set(0);
  }

  ngOnDestroy(): void {
    this.stop();
    this.navSub.unsubscribe();
  }
}
