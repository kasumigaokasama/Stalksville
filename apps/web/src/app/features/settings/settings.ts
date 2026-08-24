import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';

import { AuthService } from '../../core/auth/auth';
import { ConnectionStatusDto } from '../../core/api/api.model';

@Component({
  selector: 'stl-settings',
  styleUrl: './settings.scss',
  templateUrl: './settings.html',
})
export class Settings {
  protected readonly auth = inject(AuthService);

  private readonly statusResource = httpResource<ConnectionStatusDto>(() => '/api/v1/system/wolvesville/status');

  protected readonly status = computed(() => (this.statusResource.hasValue() ? this.statusResource.value() : null));
  protected readonly isLoading = computed(() => this.statusResource.isLoading());

  protected readonly statusClass = computed(() => {
    switch (this.status()?.status) {
      case 'Connected':
      case 'MockMode':
        return 'stl-tag--success';
      case 'InvalidKey':
        return 'stl-tag--danger';
      case 'NotConfigured':
        return 'stl-tag--warning';
      default:
        return 'stl-tag--warning';
    }
  });

  protected readonly capabilityClass = (state: string): string => {
    switch (state) {
      case 'Available':
        return 'stl-tag--success';
      case 'Disabled':
        return 'stl-tag--danger';
      default:
        return 'stl-tag--warning';
    }
  };
}
