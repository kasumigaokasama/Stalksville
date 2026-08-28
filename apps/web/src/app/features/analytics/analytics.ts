import { httpResource } from '@angular/common/http';
import { Component, computed, signal } from '@angular/core';

import { AnalyticsSeriesDto, AnalyticsSummaryDto } from '../../core/api/api.model';
import { formatNumber } from '../../shared/util/format';
import { Skeleton } from '../../shared/ui/skeleton';

/** Activity analytics (plan §37) with dependency-free CSS bar charts. */
@Component({
  selector: 'stl-analytics',
  imports: [Skeleton],
  styleUrl: './analytics.scss',
  templateUrl: './analytics.html',
})
export class Analytics {
  protected readonly days = signal(30);

  private readonly summaryResource = httpResource<AnalyticsSummaryDto>(() =>
    `/api/v1/analytics/summary?days=${this.days()}`,
  );

  protected retry(): void {
    this.summaryResource.reload();
  }

  protected readonly summary = computed(() => (this.summaryResource.hasValue() ? this.summaryResource.value() : null));
  protected readonly isLoading = computed(() => this.summaryResource.isLoading());
  protected readonly failed = computed(() => this.summaryResource.error() !== undefined && !this.summary());

  protected readonly cards = computed(() => {
    const s = this.summary();
    return [
      { label: 'Players tracked', value: formatNumber(s?.playersTracked ?? null) },
      { label: 'Clans tracked', value: formatNumber(s?.clansTracked ?? null) },
      { label: 'Snapshots', value: formatNumber(s?.snapshotsCollected ?? null) },
      { label: 'Changes', value: formatNumber(s?.changesDetected ?? null) },
      { label: 'Relationships', value: formatNumber(s?.relationshipsDiscovered ?? null) },
      { label: 'Active cases', value: formatNumber(s?.activeInvestigations ?? null) },
    ];
  });

  protected maxValue(series: AnalyticsSeriesDto): number {
    return Math.max(1, ...series.points.map((p) => p.value));
  }
}
