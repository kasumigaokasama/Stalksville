import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RouterLink } from '@angular/router';

import { PlayerLookupResultDto, RankedBoardDto, RankedCaptureResultDto, RankedSeasonDto } from '../../core/api/api.model';
import { AuthService } from '../../core/auth/auth';
import { formatDateTime, formatNumber } from '../../shared/util/format';

/** Wolvesville ranked leaderboard (observed) — season context plus tracked-player deep links. */
@Component({
  selector: 'stl-ranked',
  imports: [RouterLink],
  styleUrl: './ranked.scss',
  templateUrl: './ranked.html',
})
export class Ranked {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);

  protected readonly busy = signal(false);
  protected readonly note = signal<string | null>(null);
  protected readonly tracking = signal<string | null>(null);

  private readonly boardResource = httpResource<RankedBoardDto>(() => '/api/v1/ranked');
  private readonly seasonResource = httpResource<RankedSeasonDto>(() => '/api/v1/ranked/season');

  protected readonly board = computed(() => (this.boardResource.hasValue() ? this.boardResource.value() : null));
  protected readonly rows = computed(() => this.board()?.rows ?? []);
  protected readonly isLoading = computed(() => this.boardResource.isLoading());

  protected readonly season = computed(() =>
    this.seasonResource.hasValue() ? this.seasonResource.value() : null,
  );

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatNumber = formatNumber;

  protected async capture(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.note.set(null);
    try {
      const result = await firstValueFrom(
        this.http.post<RankedCaptureResultDto>('/api/v1/ranked/capture', null),
      );
      this.note.set(
        `Captured ${result.entriesStored} entries (season ${result.seasonNumber}) · ${result.rankShiftAlerts} rank-shift alert(s) raised`,
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
      await firstValueFrom(this.http.post('/api/v1/ranked/capture', null));
      this.note.set(`Imported ${username} and re-resolved the board.`);
      this.boardResource.reload();
    } catch {
      this.note.set(`Could not import ${username} — the player may have renamed.`);
    } finally {
      this.tracking.set(null);
    }
  }
}
