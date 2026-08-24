import { httpResource } from '@angular/common/http';
import { Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DashboardStatsDto } from '../../core/api/api.model';
import { fieldLabel, formatNumber, formatRelative } from '../../shared/util/format';

@Component({
  selector: 'stl-dashboard',
  imports: [RouterLink],
  styleUrl: './dashboard.scss',
  templateUrl: './dashboard.html',
})
export class Dashboard {
  private readonly stats = httpResource<DashboardStatsDto>(() => '/api/v1/system/dashboard');

  protected readonly data = computed(() => (this.stats.hasValue() ? this.stats.value() : null));
  protected readonly isLoading = computed(() => this.stats.isLoading());
  protected readonly failed = computed(() => this.stats.error() !== undefined && !this.data());

  protected readonly cards = computed(() => {
    const d = this.data();
    return [
      { label: 'Players tracked', value: formatNumber(d?.playersTracked ?? null) },
      { label: 'Clans tracked', value: formatNumber(d?.clansTracked ?? null) },
      { label: 'Snapshots collected', value: formatNumber(d?.snapshotsCollected ?? null) },
      { label: 'Changes detected', value: formatNumber(d?.changesDetected ?? null) },
      { label: 'Relationships', value: formatNumber(d?.relationshipsDiscovered ?? null) },
    ];
  });

  protected readonly fieldLabel = fieldLabel;
  protected readonly formatRelative = formatRelative;
}
