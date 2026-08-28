import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RouterLink } from '@angular/router';

import {
  HallOfFameBoardDto,
  HallOfFameCaptureResultDto,
  HallOfFameRowDto,
  PlayerLookupResultDto,
  RankedBoardDto,
  RankedCaptureResultDto,
  RankedSeasonDto,
} from '../../core/api/api.model';
import { AuthService } from '../../core/auth/auth';
import { formatDateTime, formatNumber } from '../../shared/util/format';

type View = 'leaderboard' | 'hall-of-fame';

/** Wolvesville ranked data (observed): the live leaderboard plus finished-season hall-of-fame winners. */
@Component({
  selector: 'stl-ranked',
  imports: [RouterLink],
  styleUrl: './ranked.scss',
  templateUrl: './ranked.html',
})
export class Ranked {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);

  protected readonly view = signal<View>('leaderboard');
  protected readonly hofSeason = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly note = signal<string | null>(null);
  protected readonly tracking = signal<string | null>(null);

  private readonly boardResource = httpResource<RankedBoardDto>(() =>
    this.view() === 'leaderboard' ? '/api/v1/ranked' : undefined,
  );
  private readonly seasonResource = httpResource<RankedSeasonDto>(() => '/api/v1/ranked/season');
  private readonly hallOfFameResource = httpResource<HallOfFameBoardDto>(() => {
    if (this.view() !== 'hall-of-fame') {
      return undefined;
    }
    const season = this.hofSeason();
    return season === null ? '/api/v1/ranked/hall-of-fame' : `/api/v1/ranked/hall-of-fame?season=${season}`;
  });

  protected readonly board = computed(() => (this.boardResource.hasValue() ? this.boardResource.value() : null));
  protected readonly rows = computed(() => this.board()?.rows ?? []);
  protected readonly isLoading = computed(() =>
    this.view() === 'leaderboard' ? this.boardResource.isLoading() : this.hallOfFameResource.isLoading(),
  );

  protected readonly season = computed(() =>
    this.seasonResource.hasValue() ? this.seasonResource.value() : null,
  );

  protected readonly hallOfFame = computed(() =>
    this.hallOfFameResource.hasValue() ? this.hallOfFameResource.value() : null,
  );
  protected readonly hallOfFameRows = computed(() => this.hallOfFame()?.rows ?? []);

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatNumber = formatNumber;

  protected async capture(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.note.set(null);
    try {
      if (this.view() === 'hall-of-fame') {
        const result = await firstValueFrom(
          this.http.post<HallOfFameCaptureResultDto>('/api/v1/ranked/hall-of-fame/capture', null),
        );
        this.note.set(
          `Captured ${result.entriesStored} season-${result.seasonNumber} winners · ${result.trackedWinnerAlerts} tracked-winner alert(s)`,
        );
        this.hallOfFameResource.reload();
      } else {
        const result = await firstValueFrom(
          this.http.post<RankedCaptureResultDto>('/api/v1/ranked/capture', null),
        );
        this.note.set(
          `Captured ${result.entriesStored} entries (season ${result.seasonNumber}) · ${result.rankShiftAlerts} rank-shift alert(s) raised`,
        );
        this.boardResource.reload();
      }
    } catch {
      this.note.set('Capture failed — check the Wolvesville connection in Settings.');
    } finally {
      this.busy.set(false);
    }
  }

  /** Track an untracked player straight from a board, then re-resolve the tracked flags. */
  protected async track(username: string, board: 'leaderboard' | 'hall-of-fame'): Promise<void> {
    if (this.tracking()) {
      return;
    }
    this.tracking.set(username);
    this.note.set(null);
    try {
      await firstValueFrom(
        this.http.get<PlayerLookupResultDto>(`/api/v1/players/lookup?username=${encodeURIComponent(username)}`),
      );
      await firstValueFrom(
        this.http.post(board === 'hall-of-fame' ? '/api/v1/ranked/hall-of-fame/capture' : '/api/v1/ranked/capture', null),
      );
      this.note.set(`Imported ${username} and re-resolved the board.`);
      if (board === 'hall-of-fame') {
        this.hallOfFameResource.reload();
      } else {
        this.boardResource.reload();
      }
    } catch {
      this.note.set(`Could not import ${username} — the player may have renamed.`);
    } finally {
      this.tracking.set(null);
    }
  }

  protected readonly trackRow = (row: HallOfFameRowDto): void => {
    void this.track(row.playerName, 'hall-of-fame');
  };
}
