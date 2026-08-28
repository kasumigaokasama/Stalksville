import { ChangeDetectionStrategy, Component, computed, input, numberAttribute } from '@angular/core';

/**
 * Loading placeholder rows (shared). Replaces bare "Loading…" text so list pages keep their
 * layout while data is in flight.
 */
@Component({
  selector: 'stl-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './skeleton.scss',
  templateUrl: './skeleton.html',
})
export class Skeleton {
  readonly lines = input(0, { transform: numberAttribute });

  protected readonly rows = computed(() => Array.from({ length: this.lines() }, (_, i) => i));
}
