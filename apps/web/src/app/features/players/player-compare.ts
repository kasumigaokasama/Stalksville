import { httpResource } from '@angular/common/http';
import { Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CompareResultDto, PlayerSummaryDto } from '../../core/api/api.model';

/** Side-by-side comparison with overlap analysis (plan §16) — correlation ≠ proof. */
@Component({
  selector: 'stl-player-compare',
  imports: [RouterLink],
  styleUrl: './player-compare.scss',
  templateUrl: './player-compare.html',
})
export class PlayerCompare {
  protected readonly playerA = signal('');
  protected readonly playerB = signal('');

  private readonly playersResource = httpResource<PlayerSummaryDto[]>(() => '/api/v1/players');
  protected readonly players = computed(() =>
    this.playersResource.hasValue() ? this.playersResource.value() ?? [] : [],
  );

  private readonly compareResource = httpResource<CompareResultDto>(() =>
    this.playerA() && this.playerB()
      ? `/api/v1/players/compare?a=${this.playerA()}&b=${this.playerB()}`
      : undefined,
  );

  protected readonly result = computed(() => (this.compareResource.hasValue() ? this.compareResource.value() : null));
  protected readonly isLoading = computed(() => this.compareResource.isLoading());

  protected readonly sameSelected = computed(() =>
    this.playerA() !== '' && this.playerA() === this.playerB(),
  );
}
