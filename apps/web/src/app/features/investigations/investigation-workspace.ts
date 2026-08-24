import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../core/auth/auth';
import {
  AiNarrative,
  ClanSummaryDto,
  InvestigationWorkspaceDto,
  PlayerSummaryDto,
} from '../../core/api/api.model';
import { formatDateTime, formatRelative } from '../../shared/util/format';

type TargetKind = 'player' | 'clan';

interface Candidate {
  id: string;
  label: string;
  sub: string;
}

/**
 * Investigation workspace (master plan §44): targets, notes, aggregated timeline and stats for
 * one case.
 */
@Component({
  selector: 'stl-investigation-workspace',
  imports: [RouterLink],
  styleUrl: './investigation-workspace.scss',
  templateUrl: './investigation-workspace.html',
})
export class InvestigationWorkspace {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  private readonly workspaceResource = httpResource<InvestigationWorkspaceDto>(() => `/api/v1/investigations/${this.id()}`);

  protected readonly workspace = computed(() =>
    this.workspaceResource.hasValue() ? this.workspaceResource.value() : null,
  );
  protected readonly isLoading = computed(() => this.workspaceResource.isLoading());
  protected readonly notFound = computed(() => this.workspaceResource.error() !== undefined && !this.workspace());

  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly noteDraft = signal('');
  protected readonly noteError = signal<string | null>(null);

  // ---- AI narrative (phase 6) ----
  protected readonly narrative = signal<AiNarrative | null>(null);
  protected readonly explaining = signal(false);

  protected async explain(): Promise<void> {
    if (this.explaining()) {
      return;
    }
    this.explaining.set(true);
    try {
      const result = await firstValueFrom(this.http.post<AiNarrative>(`/api/v1/investigations/${this.id()}/explain`, null));
      this.narrative.set(result);
    } catch {
      this.narrative.set(null);
      this.actionError.set('Explanation failed — try again.');
    } finally {
      this.explaining.set(false);
    }
  }

  /** Fetches the report as a blob (auth header included) and triggers a browser download. */
  protected async export(format: 'md' | 'csv' | 'json'): Promise<void> {
    const blob = await firstValueFrom(
      this.http.get(`/api/v1/investigations/${this.id()}/export?format=${format}`, { responseType: 'blob' }),
    );
    const url = URL.createObjectURL(blob as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `stalksville-case-${this.workspace()?.investigation.caseNumber.toString().padStart(4, '0')}.${format}`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  // ---- target picker ----
  protected readonly targetKind = signal<TargetKind>('player');
  protected readonly candidateQuery = signal('');

  private readonly playerCandidates = httpResource<PlayerSummaryDto[]>(() =>
    this.candidateQuery().trim().length >= 1 && this.targetKind() === 'player'
      ? `/api/v1/players?query=${encodeURIComponent(this.candidateQuery().trim())}`
      : undefined,
  );

  private readonly clanCandidates = httpResource<ClanSummaryDto[]>(() =>
    this.candidateQuery().trim().length >= 1 && this.targetKind() === 'clan'
      ? `/api/v1/clans/local?query=${encodeURIComponent(this.candidateQuery().trim())}`
      : undefined,
  );

  protected readonly candidates = computed<Candidate[]>(() => {
    if (this.targetKind() === 'player') {
      const players = this.playerCandidates.hasValue() ? this.playerCandidates.value() ?? [] : [];
      return players.map((p) => ({ id: p.id, label: p.username, sub: `ID ${p.wolvesvillePlayerId}` }));
    }
    const clans = this.clanCandidates.hasValue() ? this.clanCandidates.value() ?? [] : [];
    return clans
      .filter((c) => c.name !== null)
      .map((c) => ({ id: c.id, label: c.name ?? '', sub: `ID ${c.wolvesvilleClanId.slice(0, 13)}…` }));
  });

  protected readonly candidatesLoading = computed(() =>
    this.targetKind() === 'player' ? this.playerCandidates.isLoading() : this.clanCandidates.isLoading(),
  );

  protected async addTarget(candidateId: string): Promise<void> {
    await this.mutate(
      () => firstValueFrom(this.http.post<InvestigationWorkspaceDto>(
        `/api/v1/investigations/${this.id()}/targets`,
        { entityType: this.targetKind(), entityId: candidateId },
      )),
      () => this.candidateQuery.set(''),
    );
  }

  protected async removeTarget(targetId: string): Promise<void> {
    await this.mutate(
      () => firstValueFrom(this.http.delete<InvestigationWorkspaceDto>(`/api/v1/investigations/${this.id()}/targets/${targetId}`)),
    );
  }

  protected async addNote(): Promise<void> {
    const content = this.noteDraft().trim();
    if (!content) {
      return;
    }
    await this.mutate(
      () => firstValueFrom(this.http.post<InvestigationWorkspaceDto>(`/api/v1/investigations/${this.id()}/notes`, { content })),
      () => {
        this.noteDraft.set('');
        this.noteError.set(null);
      },
    );
  }

  protected async setStatus(archived: boolean): Promise<void> {
    await this.mutate(
      () => firstValueFrom(this.http.post<InvestigationWorkspaceDto>(
        `/api/v1/investigations/${this.id()}/${archived ? 'archive' : 'reopen'}`,
        null,
      )),
    );
  }

  private async mutate(request: () => Promise<InvestigationWorkspaceDto>, onSuccess?: () => void): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await request();
      onSuccess?.();
      this.workspaceResource.reload();
    } catch (err) {
      const detail = (err as { error?: { detail?: string } })?.error?.detail;
      this.actionError.set(detail ?? 'Operation failed.');
    } finally {
      this.busy.set(false);
    }
  }

  protected entityLink(entityType: string, entityId: string): string {
    return entityType === 'player' ? `/players/${entityId}` : `/clans/${entityId}`;
  }

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;
}
