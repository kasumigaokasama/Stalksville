import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ClanDossierDto } from '../../core/api/api.model';
import { formatDateTime, formatNumber, formatRelative } from '../../shared/util/format';

@Component({
  selector: 'stl-clan-detail',
  imports: [RouterLink],
  styleUrl: './clan-detail.scss',
  templateUrl: './clan-detail.html',
})
export class ClanDetail {
  readonly id = input.required<string>();

  private readonly clanResource = httpResource<ClanDossierDto>(() => `/api/v1/clans/${this.id()}`);

  protected readonly dossier = computed(() => (this.clanResource.hasValue() ? this.clanResource.value() : null));
  protected readonly isLoading = computed(() => this.clanResource.isLoading());
  protected readonly notFound = computed(() => this.clanResource.error() !== undefined && !this.dossier());

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;
  protected readonly formatNumber = formatNumber;
}
