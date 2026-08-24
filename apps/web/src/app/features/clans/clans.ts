import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth';
import { ClanDossierDto, ClanSummaryDto } from '../../core/api/api.model';
import { formatRelative } from '../../shared/util/format';

@Component({
  selector: 'stl-clans',
  imports: [RouterLink],
  styleUrl: './clans.scss',
  templateUrl: './clans.html',
})
export class Clans {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly searchName = signal('');
  protected readonly searchResults = signal<ClanSummaryDto[] | null>(null);
  protected readonly searching = signal(false);
  protected readonly searchError = signal<string | null>(null);
  protected readonly importing = signal<string | null>(null);
  protected readonly importNote = signal<string | null>(null);

  private readonly localResource = httpResource<ClanSummaryDto[]>(() =>
    this.query().trim()
      ? `/api/v1/clans/local?query=${encodeURIComponent(this.query().trim())}`
      : '/api/v1/clans/local',
  );

  /** Local filter (applied live). */
  protected readonly query = signal('');

  protected readonly localClans = computed(() =>
    this.localResource.hasValue() ? this.localResource.value() ?? [] : [],
  );
  protected readonly isLoadingLocal = computed(() => this.localResource.isLoading());

  protected search(): void {
    const name = this.searchName().trim();
    if (!name || this.searching()) {
      return;
    }

    this.searching.set(true);
    this.searchError.set(null);
    this.searchResults.set(null);

    this.http
      .get<ClanSummaryDto[]>(`/api/v1/clans/search?name=${encodeURIComponent(name)}`)
      .subscribe({
        next: (results) => {
          this.searching.set(false);
          this.searchResults.set(results);
        },
        error: (err) => {
          this.searching.set(false);
          this.searchError.set(err?.error?.detail ?? 'Clan search failed.');
        },
      });
  }

  /** Import (or re-import): snapshots every member through the player pipeline. */
  protected importClan(wolvesvilleClanId: string): void {
    if (this.importing()) {
      return;
    }

    this.importing.set(wolvesvilleClanId);
    this.importNote.set(null);

    this.http
      .post<ClanDossierDto>(`/api/v1/clans/${encodeURIComponent(wolvesvilleClanId)}/import`, null)
      .subscribe({
        next: (dossier) => {
          this.importing.set(null);
          this.importNote.set(
            `${dossier.clan.name} imported — ${dossier.knownMembers.length} members snapshotted.`,
          );
          this.localResource.reload();
          void this.router.navigate(['/clans', dossier.clan.id]);
        },
        error: (err) => {
          this.importing.set(null);
          this.importNote.set(err?.error?.detail ?? 'Import failed.');
        },
      });
  }

  protected formatRelative = formatRelative;
}
