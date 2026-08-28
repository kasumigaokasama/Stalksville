import { httpResource } from '@angular/common/http';
import {
  Component,
  ElementRef,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import cytoscape from 'cytoscape';

import {
  GraphAnalyticsDto,
  GraphDto,
    GraphEdgeDto,
  GraphNodeDto,
  GraphPathsDto,
  PlayerSummaryDto,
} from '../../core/api/api.model';
import { formatDateTime } from '../../shared/util/format';

/**
 * Relationship graph (plan §12/§44). Nodes are tracked players and clans; edges are derived
 * relationships with confidence — current memberships solid, historical dashed. Optionally
 * scoped to one investigation's ego network via ?investigationId=.
 */
@Component({
  selector: 'stl-graph',
  imports: [RouterLink],
  styleUrl: './graph.scss',
  templateUrl: './graph.html',
})
export class Graph implements OnDestroy {
  /** Query-param binding: ?investigationId=… scopes the graph to a case's ego network. */
  readonly investigationId = input<string>();

  private readonly canvas = viewChild.required<ElementRef<HTMLDivElement>>('canvas');

  private readonly graphResource = httpResource<GraphDto>(() =>
    this.investigationId() ? `/api/v1/graph?investigationId=${this.investigationId()}` : '/api/v1/graph',
  );

  private readonly analyticsResource = httpResource<GraphAnalyticsDto>(() =>
    this.investigationId()
      ? `/api/v1/graph/analytics?investigationId=${this.investigationId()}`
      : '/api/v1/graph/analytics',
  );

  protected readonly graph = computed(() => (this.graphResource.hasValue() ? this.graphResource.value() : null));
  protected readonly analytics = computed(() =>
    this.analyticsResource.hasValue() ? this.analyticsResource.value() : null,
  );
  protected readonly isLoading = computed(() => this.graphResource.isLoading());

  protected readonly selectedNode = signal<GraphNodeDto | null>(null);
  protected readonly selectedEdge = signal<GraphEdgeDto | null>(null);

  // ---- path finding ----
  private readonly playersResource = httpResource<PlayerSummaryDto[]>(() => '/api/v1/players');
  protected readonly players = computed(() =>
    this.playersResource.hasValue() ? this.playersResource.value() ?? [] : [],
  );

  protected readonly pathFrom = signal('');
  protected readonly pathTo = signal('');
  private readonly pathsResource = httpResource<GraphPathsDto>(() =>
    this.pathFrom() && this.pathTo()
      ? `/api/v1/graph/paths?from=${this.pathFrom()}&to=${this.pathTo()}`
      : undefined,
  );

  protected readonly paths = computed(() => {
    const paths = this.pathsResource.hasValue() ? this.pathsResource.value() : null;
    return paths && this.pathFrom() && this.pathTo() ? paths.paths : null;
  });

  private cy: cytoscape.Core | null = null;

  constructor() {
    effect(() => {
      const data = this.graph();
      if (data) {
        this.render(data, this.communityByNode());
      }
    });
  }

  ngOnDestroy(): void {
    this.cy?.destroy();
    this.cy = null;
  }

  private readonly communityPalette = ['#f472b6', '#38bdf8', '#facc15', '#a3e635', '#fb923c', '#c084fc', '#2dd4bf', '#f87171'];

  private readonly communityByNode = computed(() => {
    const map = new Map<string, number>();
    for (const community of this.analytics()?.communities ?? []) {
      for (const memberId of community.memberIds) {
        map.set(memberId, community.index);
      }
    }
    return map;
  });

  private render(data: GraphDto, communityByNode: ReadonlyMap<string, number>): void {
    this.cy?.destroy();

    const nodeById = new Map(data.nodes.map((n) => [n.id, n]));

    this.cy = cytoscape({
      container: this.canvas().nativeElement,
      wheelSensitivity: 0.2,
      elements: [
        ...data.nodes.map((node) => ({
          data: { id: node.id, label: node.label },
          classes: node.type,
        })),
        ...data.edges.map((edge) => ({
          data: {
            id: edge.id,
            source: edge.source,
            target: edge.target,
            label: `${edge.type} · ${(edge.confidence * 100).toFixed(0)}%`,
          },
          classes: edge.type === 'FRIEND_OF'
            ? (edge.isCurrent ? 'edge-friend edge-current' : 'edge-friend edge-historical')
            : edge.isCurrent ? 'edge-current' : 'edge-historical',
        })),
      ],
      style: [
        { selector: 'node.player', style: {
          'background-color': '#7c9aff',
          'border-width': 2,
          'border-color': '#0b0e14',
          'label': 'data(label)',
          'color': '#e6e9f0',
          'font-size': 10,
          'text-valign': 'bottom',
          'text-margin-y': 6,
          'width': 22,
          'height': 22,
        } },
        { selector: 'node.clan', style: {
          'background-color': '#4ade80',
          'shape': 'round-rectangle',
          'border-width': 2,
          'border-color': '#0b0e14',
          'label': 'data(label)',
          'color': '#8b93a7',
          'font-size': 10,
          'text-valign': 'bottom',
          'text-margin-y': 6,
          'width': 26,
          'height': 18,
        } },
        { selector: 'node:selected', style: { 'border-color': '#e6e9f0', 'border-width': 3 } },
        { selector: 'edge.edge-current', style: {
          'line-color': '#c084fc',
          'width': 2,
          'curve-style': 'haystack',
        } },
        { selector: 'edge.edge-historical', style: {
          'line-color': '#7a5fa0',
          'width': 1.5,
          'line-style': 'dashed',
          'curve-style': 'haystack',
        } },
        { selector: 'edge.edge-friend', style: {
          'line-color': '#2dd4bf',
          'width': 1.5,
          'curve-style': 'haystack',
        } },
        { selector: 'edge:selected', style: { 'line-color': '#e6e9f0', 'width': 3 } },
      ],
      layout: { name: 'cose', animate: true, padding: 30 } as never,
    });

    // Community rings: one border color per detected community (single communities stay neutral).
    if (communityByNode.size > 0 && (this.analytics()?.communities.length ?? 0) > 1) {
      for (const [nodeId, index] of communityByNode) {
        this.cy.getElementById(nodeId).style('border-color', this.communityPalette[index % this.communityPalette.length]);
        this.cy.getElementById(nodeId).style('border-width', 3);
      }
    }

    this.cy.on('tap', 'node', (event) => {
      const node = nodeById.get(event.target.id());
      this.selectedEdge.set(null);
      this.selectedNode.set(node ?? null);
    });

    this.cy.on('tap', 'edge', (event) => {
      const edge = data.edges.find((e) => e.id === event.target.id());
      this.selectedNode.set(null);
      this.selectedEdge.set(edge ?? null);
    });

    this.cy.on('tap', (event) => {
      if (event.target === this.cy) {
        this.selectedNode.set(null);
        this.selectedEdge.set(null);
      }
    });
  }

  protected entityLink(node: { type: string; id: string }): string {
    return node.type === 'player' ? `/players/${node.id}` : `/clans/${node.id}`;
  }

  protected readonly formatDateTime = formatDateTime;
}
