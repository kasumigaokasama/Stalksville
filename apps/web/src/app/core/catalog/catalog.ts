import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';

import { CatalogItemDto } from '../api/api.model';

/**
 * Wolvesville cosmetics catalogs (observed reference data): short badge/profile-icon ids
 * resolved to display names so dossiers and change feeds read like names, not codes.
 */
@Injectable({ providedIn: 'root' })
export class Catalog {
  private readonly badgesResource = httpResource<CatalogItemDto[]>(() => '/api/v1/catalog?kind=badge');
  private readonly iconsResource = httpResource<CatalogItemDto[]>(() => '/api/v1/catalog?kind=profileIcon');

  private readonly badgeNames = computed(() =>
    new Map((this.badgesResource.hasValue() ? this.badgesResource.value() ?? [] : []).map((i) => [i.externalId, i.name])),
  );

  private readonly iconNames = computed(() =>
    new Map((this.iconsResource.hasValue() ? this.iconsResource.value() ?? [] : []).map((i) => [i.externalId, i.name])),
  );

  /** Badge display name, falling back to the raw id when the catalog has not loaded it. */
  badgeLabel(id: string): string {
    return this.badgeNames().get(id) ?? id;
  }

  /** Comma-joined badge labels for a list of ids. */
  badgeList(ids: readonly string[]): string {
    return ids.map((id) => this.badgeLabel(id)).join(', ');
  }

  /** Profile icon display name; null when the id is unknown or not yet loaded. */
  iconLabel(id: string | null | undefined): string | null {
    if (!id) {
      return null;
    }
    return this.iconNames().get(id) ?? null;
  }
}
