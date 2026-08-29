import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Stalksville wolf-head brand mark, inlined so it themes with the app: the badge follows
 * the accent, the head follows the surface, the eyes carry the "observed" sky hue.
 * Keep the geometry in sync with public/favicon.svg and scripts/brand-assets.mjs.
 */
@Component({
  selector: 'stl-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg viewBox="0 0 64 64" aria-hidden="true" focusable="false">
      <rect width="64" height="64" rx="14" fill="var(--stl-accent)" />
      <path d="M16 11 L26 20 L48 11 L51 30 L41 45 L32 57 L23 45 L13 30 Z" fill="var(--stl-bg)" />
      <circle cx="25" cy="30" r="3.2" fill="var(--stl-observed)" />
      <circle cx="39" cy="30" r="3.2" fill="var(--stl-observed)" />
    </svg>
  `,
  styles: `
    :host {
      display: block;
      width: 28px;
      height: 28px;
      flex: none;
    }

    svg {
      display: block;
      width: 100%;
      height: 100%;
    }
  `,
})
export class BrandMark {}
