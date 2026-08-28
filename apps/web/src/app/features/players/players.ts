import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth';
import { PlayerLookupResultDto, PlayerSummaryDto, WatchedPlayerDto } from '../../core/api/api.model';
import { formatRelative } from '../../shared/util/format';

@Component({
  selector: 'stl-players',
  imports: [RouterLink],
  styleUrl: './players.scss',
  templateUrl: './players.html',
})
export class Players {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  /** Local search term, applied on submit (or per keystroke for tracked players). */
  protected readonly query = signal('');
  protected readonly watchedOnly = signal(false);
  protected readonly lookupName = signal('');
  protected readonly lookupBusy = signal(false);
  protected readonly lookupError = signal<string | null>(null);
  protected readonly lookupNote = signal<string | null>(null);

  private readonly playersResource = httpResource<PlayerSummaryDto[]>(() =>
    this.query().trim()
      ? `/api/v1/players?query=${encodeURIComponent(this.query().trim())}`
      : '/api/v1/players',
  );

  private readonly watchlistResource = httpResource<WatchedPlayerDto[]>(() => '/api/v1/watchlist');

  protected readonly watchedIds = computed(() =>
    new Set((this.watchlistResource.hasValue() ? this.watchlistResource.value() ?? [] : []).map((w) => w.id)),
  );

  protected readonly players = computed(() => {
    const all = this.playersResource.hasValue() ? this.playersResource.value() ?? [] : [];
    return this.watchedOnly() ? all.filter((p) => this.watchedIds().has(p.id)) : all;
  });
  protected readonly isLoading = computed(() => this.playersResource.isLoading());

  /** Live exact-username lookup against Wolvesville — imports the player. */
  protected lookup(): void {
    const name = this.lookupName().trim();
    if (!name || this.lookupBusy()) {
      return;
    }

    this.lookupBusy.set(true);
    this.lookupError.set(null);
    this.lookupNote.set(null);

    this.http.get<PlayerLookupResultDto>(`/api/v1/players/lookup?username=${encodeURIComponent(name)}`).subscribe({
      next: (result) => {
        this.lookupBusy.set(false);
        this.lookupNote.set(
          result.wasReobserved
            ? `${result.dossier.player.username} was already current (unchanged snapshot observed again).`
            : `${result.dossier.player.username} imported — ${result.changesDetectedInThisObservation} change(s) recorded.`,
        );
        void this.router.navigate(['/players', result.dossier.player.id]);
      },
      error: (err) => {
        this.lookupBusy.set(false);
        this.lookupError.set(
          err?.error?.detail ?? 'Lookup failed. Wolvesville usernames must match exactly.',
        );
      },
    });
  }

  protected formatRelative = formatRelative;
}
