import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RouterLink } from '@angular/router';

import { HighscoreBoardDto, PlayerLookupResultDto } from '../../core/api/api.model';
import { AuthService } from '../../core/auth/auth';
import { formatDateTime, formatNumber } from '../../shared/util/format';

type Period = 'alltime' | 'monthly' | 'weekly' | 'daily';

/** Wolvesville top-100 XP boards (observed) — tracked players deep-link to their dossiers. */
@Component({
  selector: 'stl-highscores',
  imports: [RouterLink],
  styleUrl: './highscores.scss',
  templateUrl: './highscores.html',
})
export class Highscores {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);

  protected readonly period = signal<Period>('alltime');
  protected readonly busy = signal(false);
  protected readonly note = signal<string | null>(null);
  protected readonly tracking = signal<string | null>(null);

  private readonly boardResource = httpResource<HighscoreBoardDto>(() => `/api/v1/highscores?period=${this.period()}`);

  protected readonly board = computed(() => (this.boardResource.hasValue() ? this.boardResource.value() : null));
  protected readonly rows = computed(() => (this.board()?.rows ?? []));
  protected readonly isLoading = computed(() => this.boardResource.isLoading());

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatNumber = formatNumber;

  protected readonly periods: { value: Period; label: string }[] = [
    { value: 'alltime', label: 'All-time' },
    { value: 'monthly', label: 'Monthly' },
    { value: 'weekly', label: 'Weekly' },
    { value: 'daily', label: 'Daily' },
  ];

  protected async capture(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.note.set(null);
    try {
      const result = await firstValueFrom(
        this.http.post<import('../../core/api/api.model').HighscoreCaptureResultDto>('/api/v1/highscores/capture', null),
      );
      this.note.set(
        `Captured ${result.entriesStored} entries · ${result.rankShiftAlerts} rank-shift alert(s) raised`,
      );
      this.boardResource.reload();
    } catch {
      this.note.set('Capture failed — check the Wolvesville connection in Settings.');
    } finally {
      this.busy.set(false);
    }
  }

  /** Track an untracked player straight from the board, then re-resolve the tracked flags. */
  protected async track(username: string): Promise<void> {
    if (this.tracking()) {
      return;
    }
    this.tracking.set(username);
    this.note.set(null);
    try {
      await firstValueFrom(
        this.http.get<PlayerLookupResultDto>(`/api/v1/players/lookup?username=${encodeURIComponent(username)}`),
      );
      await firstValueFrom(this.http.post('/api/v1/highscores/capture', null));
      this.note.set(`Imported ${username} and re-resolved the board.`);
      this.boardResource.reload();
    } catch {
      this.note.set(`Could not import ${username} — the player may have renamed.`);
    } finally {
      this.tracking.set(null);
    }
  }
}
