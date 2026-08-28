import { DOCUMENT } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';

const THEME_KEY = 'stl_theme';

/**
 * Dark-first theme toggle: 'dark' is the default, 'light' is opt-in. The choice persists in
 * localStorage and applies the [data-theme] attribute on <html>, which swaps the design tokens.
 */
@Injectable({ providedIn: 'root' })
export class Theme {
  private readonly document = inject(DOCUMENT);

  readonly current = signal<'dark' | 'light'>(restore());

  constructor() {
    this.apply(this.current());
  }

  toggle(): void {
    this.set(this.current() === 'dark' ? 'light' : 'dark');
  }

  private set(theme: 'dark' | 'light'): void {
    this.current.set(theme);
    localStorage.setItem(THEME_KEY, theme);
    this.apply(theme);
  }

  private apply(theme: 'dark' | 'light'): void {
    this.document.documentElement.setAttribute('data-theme', theme);
  }
}

function restore(): 'dark' | 'light' {
  try {
    return localStorage.getItem(THEME_KEY) === 'light' ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}
