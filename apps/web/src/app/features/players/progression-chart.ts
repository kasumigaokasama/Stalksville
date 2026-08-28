import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { ProgressionPointDto } from '../../core/api/api.model';

interface Series {
  readonly key: keyof Pick<ProgressionPointDto, 'level' | 'wins' | 'gamesPlayed'>;
  readonly label: string;
  readonly color: string;
}

/**
 * Minimal SVG multi-line chart over the observed progression series. No charting dependency —
 * points map into a fixed viewBox, each series keeps its own scale, and every dot is the value
 * of one real snapshot (hover shows the raw numbers).
 */
@Component({
  selector: 'stl-progression-chart',
  imports: [DatePipe],
  styleUrl: './progression-chart.scss',
  templateUrl: './progression-chart.html',
})
export class ProgressionChart {
  readonly points = input.required<ProgressionPointDto[]>();

  private readonly series: Series[] = [
    { key: 'level', label: 'Level', color: 'var(--stl-accent)' },
    { key: 'wins', label: 'Wins', color: '#34d399' },
    { key: 'gamesPlayed', label: 'Games', color: '#fbbf24' },
  ];

  protected readonly width = 640;
  protected readonly height = 220;
  protected readonly pad = 28;

  protected readonly hasData = computed(() => this.points().some((p) => this.series.some((s) => (p[s.key] ?? 0) > 0)));

  protected readonly paths = computed(() => {
    const points = this.points();
    if (points.length === 0) {
      return [];
    }

    const step = points.length > 1 ? (this.width - this.pad * 2) / (points.length - 1) : 0;

    return this.series.map((series) => {
      const values = points.map((p) => (p[series.key] as number | null) ?? 0);
      const max = Math.max(1, ...values);
      const coords = points.map((point, index) => {
        const value = (point[series.key] as number | null) ?? 0;
        const x = this.pad + index * step;
        const y = this.height - this.pad - (value / max) * (this.height - this.pad * 2);
        return { x, y, value, capturedAt: point.capturedAt };
      });

      const line = coords.map((c) => `${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ');
      return { series, line, coords, max };
    });
  });

  protected readonly legend = this.series;
}
