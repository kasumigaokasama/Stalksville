import { DialogRef } from '@angular/cdk/dialog';
import { httpResource } from '@angular/common/http';
import { Component, computed, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { PlayerLookupResultDto, SearchHitDto } from '../api/api.model';

export interface PaletteAction {
  kind: 'navigate' | 'player' | 'search';
  label: string;
  target: string;
  icon?: string;
  sub?: string;
  tag?: string;
}

/**
 * CTRL+K command palette. Free text searches the whole workspace (tracked players and clans by
 * name, active investigations by title/description/notes via PostgreSQL FTS); the prefix
 * `player:<username>` performs a live Wolvesville lookup that imports the player.
 * Keyboard: ↑/↓ move, Home/End jump, ↵ opens the active result, esc closes.
 */
@Component({
  selector: 'stl-command-palette',
  imports: [],
  styleUrl: './command-palette.scss',
  templateUrl: './command-palette.html',
})
export class CommandPalette {
  private readonly dialogRef = inject(DialogRef);
  private readonly router = inject(Router);

  protected readonly query = signal('');
  protected readonly activeIndex = signal(0);
  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('input');

  private readonly navigateActions: PaletteAction[] = [
    { kind: 'navigate', label: 'Overview', target: '/dashboard', icon: '→' },
    { kind: 'navigate', label: 'Players', target: '/players', icon: '→' },
    { kind: 'navigate', label: 'Highscores', target: '/highscores', icon: '→' },
    { kind: 'navigate', label: 'Ranked', target: '/ranked', icon: '→' },
    { kind: 'navigate', label: 'Clans', target: '/clans', icon: '→' },
    { kind: 'navigate', label: 'Investigations', target: '/investigations', icon: '→' },
    { kind: 'navigate', label: 'Graph', target: '/graph', icon: '→' },
    { kind: 'navigate', label: 'Timeline', target: '/timeline', icon: '→' },
    { kind: 'navigate', label: 'Alerts', target: '/alerts', icon: '→' },
    { kind: 'navigate', label: 'Analytics', target: '/analytics', icon: '→' },
    { kind: 'navigate', label: 'Compare players', target: '/players/compare', icon: '→' },
    { kind: 'navigate', label: 'Settings — Wolvesville connection', target: '/settings', icon: '→' },
  ];

  // ---- live Wolvesville lookup via player: prefix ----

  private readonly playerQuery = computed(() => {
    const q = this.query().trim();
    if (!q.toLowerCase().startsWith('player:')) {
      return null;
    }
    const name = q.slice('player:'.length).trim();
    return name.length >= 2 ? name : null;
  });

  private readonly playerLookup = httpResource<PlayerLookupResultDto>(() => {
    const name = this.playerQuery();
    return name === null ? undefined : `/api/v1/players/lookup?username=${encodeURIComponent(name)}`;
  });

  protected readonly playerResult = computed(() =>
    this.playerLookup.hasValue() ? this.playerLookup.value() : null,
  );

  protected readonly isLoadingPlayer = computed(() => this.playerLookup.isLoading());

  protected readonly error = computed(() => {
    const err = this.playerLookup.error();
    if (!err || this.playerQuery() === null) {
      return null;
    }
    return 'No player found for that exact username (Wolvesville lookups are exact-match).';
  });

  // ---- workspace search for anything that is not a player: lookup ----

  protected readonly searchQuery = computed(() => {
    const q = this.query().trim();
    if (q.length < 2 || q.toLowerCase().startsWith('player:')) {
      return null;
    }
    return q;
  });

  private readonly searchResource = httpResource<SearchHitDto[]>(() => {
    const q = this.searchQuery();
    return q === null ? undefined : `/api/v1/search?q=${encodeURIComponent(q)}`;
  });

  protected readonly searchHits = computed(() => {
    if (this.searchQuery() === null) {
      return [];
    }
    return this.searchResource.hasValue() ? this.searchResource.value() ?? [] : [];
  });

  protected readonly isLoadingSearch = computed(() => this.searchResource.isLoading());

  protected readonly filteredNav = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (q.length > 0) {
      return [];
    }
    return this.navigateActions;
  });

  /** One flat, ordered result list so keyboard and pointer share the same model. */
  protected readonly results = computed<PaletteAction[]>(() => {
    if (this.playerQuery() !== null) {
      const player = this.playerResult();
      return player
        ? [{
            kind: 'player',
            label: player.dossier.player.username,
            target: player.dossier.player.id,
            icon: '👤',
            tag: 'open dossier',
            sub: player.dossier.observed?.wolvesvillePlayerId,
          }]
        : [];
    }

    const q = this.searchQuery();
    if (q !== null) {
      return this.searchHits().map((hit) => ({
        kind: 'search' as const,
        label: hit.title,
        target: this.hitTarget(hit),
        icon: this.hitIcon(hit),
        sub: hit.subtitle ?? undefined,
        tag: hit.type,
      }));
    }

    return this.filteredNav();
  });

  protected readonly activeId = computed(() =>
    this.results().length > 0 ? `palette-option-${this.activeIndex()}` : null,
  );

  protected hitTarget(hit: SearchHitDto): string {
    return hit.type === 'player' ? `/players/${hit.id}` : hit.type === 'clan' ? `/clans/${hit.id}` : `/investigations/${hit.id}`;
  }

  protected hitIcon(hit: SearchHitDto): string {
    return hit.type === 'player' ? '👤' : hit.type === 'clan' ? '🏠' : '🗂';
  }

  ngAfterViewInit(): void {
    this.input().nativeElement.focus();
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.activeIndex.set(0);
  }

  protected onKey(event: KeyboardEvent): void {
    const count = this.results().length;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (count > 0) {
          this.activeIndex.set((this.activeIndex() + 1) % count);
        }
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (count > 0) {
          this.activeIndex.set((this.activeIndex() - 1 + count) % count);
        }
        break;
      case 'Home':
        event.preventDefault();
        this.activeIndex.set(0);
        break;
      case 'End':
        event.preventDefault();
        if (count > 0) {
          this.activeIndex.set(count - 1);
        }
        break;
      case 'Enter':
        event.preventDefault();
        this.runActive();
        break;
      case 'Escape':
        event.preventDefault();
        this.close();
        break;
    }
  }

  protected run(action: PaletteAction): void {
    this.dialogRef.close();
    if (action.kind === 'navigate') {
      void this.router.navigateByUrl(action.target);
    } else if (action.kind === 'player') {
      void this.router.navigateByUrl(`/players/${action.target}`);
    } else {
      void this.router.navigateByUrl(action.target);
    }
  }

  private runActive(): void {
    const action = this.results()[this.activeIndex()];
    if (action) {
      this.run(action);
    }
  }

  protected close(): void {
    this.dialogRef.close();
  }
}
