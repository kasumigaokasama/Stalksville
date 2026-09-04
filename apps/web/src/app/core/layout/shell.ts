import { Dialog } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../auth/auth';
import { UnreadAlerts } from '../alerts/unread-alerts';
import { BrandMark } from './brand-mark';
import { Theme } from './theme';
import { CommandPalette, type PaletteAction } from './command-palette';

@Component({
  selector: 'stl-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, BrandMark],
  styleUrl: './shell.scss',
  templateUrl: './shell.html',
  host: {
    '(window:keydown)': 'onKeydown($event)',
  },
})
export class Shell {
  private readonly dialog = inject(Dialog);

  protected readonly auth = inject(AuthService);
  protected readonly unreadAlerts = inject(UnreadAlerts);
  protected readonly theme = inject(Theme);

  /** Mobile drawer state — desktop (>=900px) keeps the static sidebar. */
  protected readonly drawerOpen = signal(false);

  protected readonly nav = [
    { path: '/dashboard', label: 'Overview' },
    { path: '/players', label: 'Players' },
    { path: '/highscores', label: 'Highscores' },
    { path: '/ranked', label: 'Ranked' },
    { path: '/clans', label: 'Clans' },
    { path: '/investigations', label: 'Investigations' },
    { path: '/graph', label: 'Graph' },
    { path: '/timeline', label: 'Timeline' },
    { path: '/alerts', label: 'Alerts' },
    { path: '/analytics', label: 'Analytics' },
    { path: '/settings', label: 'Settings' },
    ...(this.auth.canAdmin() ? [{ path: '/scans', label: 'Scans' }, { path: '/admin', label: 'Admin' }] : []),
  ];

  protected onKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.openPalette();
    }
  }

  protected openPalette(): void {
    this.dialog.open<PaletteAction, unknown, CommandPalette>(CommandPalette, {
      hasBackdrop: true,
      panelClass: 'stl-palette-panel',
      width: '560px',
    });
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected logout(): void {
    this.auth.logout();
  }
}
