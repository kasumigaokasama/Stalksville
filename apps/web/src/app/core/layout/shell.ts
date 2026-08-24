import { Dialog } from '@angular/cdk/dialog';
import { Component, HostListener, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../auth/auth';
import { CommandPalette, type PaletteAction } from './command-palette';

@Component({
  selector: 'stl-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  styleUrl: './shell.scss',
  templateUrl: './shell.html',
})
export class Shell {
  private readonly dialog = inject(Dialog);

  protected readonly auth = inject(AuthService);

  protected readonly nav = [
    { path: '/dashboard', label: 'Overview' },
    { path: '/players', label: 'Players' },
    { path: '/clans', label: 'Clans' },
    { path: '/investigations', label: 'Investigations' },
    { path: '/graph', label: 'Graph' },
    { path: '/timeline', label: 'Timeline' },
    { path: '/analytics', label: 'Analytics' },
    { path: '/settings', label: 'Settings' },
  ];

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
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

  protected logout(): void {
    this.auth.logout();
  }
}
