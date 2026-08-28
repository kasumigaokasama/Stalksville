import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth';
import {
  ChangeDto,
  ExposureResultDto,
  InsightDto,
  PlayerDossierDto,
  PlayerLookupResultDto,
  ProgressionDto,
  SnapshotDto,
} from '../../core/api/api.model';
import { fieldLabel, formatDateTime, formatNumber, formatRelative } from '../../shared/util/format';
import { ProgressionChart } from './progression-chart';

type Tab = 'overview' | 'identity' | 'clans' | 'progression' | 'snapshots' | 'changes' | 'intelligence';

/** The flagship dossier: observed state on top, derived intelligence (with evidence) below. */
@Component({
  selector: 'stl-player-dossier',
  imports: [RouterLink, ProgressionChart],
  styleUrl: './player-dossier.scss',
  templateUrl: './player-dossier.html',
})
export class PlayerDossier {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  private readonly dossierResource = httpResource<PlayerDossierDto>(() => `/api/v1/players/${this.id()}`);
  private readonly snapshotsResource = httpResource<SnapshotDto[]>(() => `/api/v1/players/${this.id()}/snapshots`);
  private readonly progressionResource = httpResource<ProgressionDto>(() => `/api/v1/players/${this.id()}/progression`);
  private readonly exposureResource = httpResource<ExposureResultDto>(() => `/api/v1/players/${this.id()}/exposure`);
  private readonly insightsResource = httpResource<InsightDto[]>(() => `/api/v1/players/${this.id()}/insights`);

  protected readonly dossier = computed(() => (this.dossierResource.hasValue() ? this.dossierResource.value() : null));
  protected readonly snapshots = computed(() =>
    this.snapshotsResource.hasValue() ? this.snapshotsResource.value() ?? [] : [],
  );
  protected readonly progression = computed(() =>
    this.progressionResource.hasValue() ? this.progressionResource.value()?.points ?? [] : [],
  );
  protected readonly exposure = computed(() => (this.exposureResource.hasValue() ? this.exposureResource.value() : null));
  protected readonly insights = computed(() =>
    this.insightsResource.hasValue() ? this.insightsResource.value() ?? [] : [],
  );
  protected readonly isLoading = computed(() => this.dossierResource.isLoading());
  protected readonly notFound = computed(() => this.dossierResource.error() !== undefined && !this.dossier());

  protected readonly tab = signal<Tab>('overview');
  protected readonly refreshing = signal(false);
  protected readonly refreshNote = signal<string | null>(null);

  protected readonly tabs: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Overview' },
    { key: 'identity', label: 'Identity' },
    { key: 'clans', label: 'Clans' },
    { key: 'progression', label: 'Progression' },
    { key: 'snapshots', label: 'Snapshots' },
    { key: 'changes', label: 'Changes' },
    { key: 'intelligence', label: 'Intelligence' },
  ];

  protected refresh(): void {
    if (this.refreshing()) {
      return;
    }
    this.refreshing.set(true);
    this.refreshNote.set(null);

    this.http
      .post<PlayerLookupResultDto>(`/api/v1/players/${this.id()}/refresh`, null)
      .subscribe({
        next: (result) => {
          this.refreshing.set(false);
          this.refreshNote.set(
            result.wasReobserved
              ? 'State unchanged — existing snapshot observed again (no new snapshot stored).'
              : `${result.changesDetectedInThisObservation} change(s) detected in this observation.`,
          );
          this.dossierResource.reload();
          this.snapshotsResource.reload();
          this.progressionResource.reload();
        },
        error: () => {
          this.refreshing.set(false);
          this.refreshNote.set('Refresh failed — check the Wolvesville connection in Settings.');
        },
      });
  }

  /** Changes with evidence expanded for the selected change. */
  protected readonly selectedChange = signal<ChangeDto | null>(null);

  protected readonly fieldLabel = fieldLabel;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;
  protected readonly formatNumber = formatNumber;
}
