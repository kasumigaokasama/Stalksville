import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FormField, form, minLength, required, submit } from '@angular/forms/signals';

import { NotificationChannelDto, ScanRunDto, ScanScheduleDto } from '../../core/api/api.model';
import { formatDateTime, formatRelative } from '../../shared/util/format';

type ScanKind = 'player-refresh' | 'highscore-capture';

/**
 * Scheduled scans + change notifications (ADMIN): recurring scans stored server-side and executed
 * by the worker, plus webhook channels that fire only when a run actually detects changes.
 */
@Component({
  selector: 'stl-scans',
  imports: [FormField],
  styleUrl: './scans.scss',
  templateUrl: './scans.html',
})
export class Scans {
  private readonly http = inject(HttpClient);

  // ---- schedules ----

  private readonly schedulesResource = httpResource<ScanScheduleDto[]>(() => '/api/v1/scans/schedules');
  protected readonly schedules = computed(() => (this.schedulesResource.hasValue() ? this.schedulesResource.value() ?? [] : []));
  protected readonly schedulesLoading = computed(() => this.schedulesResource.isLoading());

  protected readonly kinds: { value: ScanKind; label: string }[] = [
    { value: 'player-refresh', label: 'Player refresh' },
    { value: 'highscore-capture', label: 'Highscore capture' },
  ];

  protected readonly creatingSchedule = signal(false);
  protected readonly scheduleError = signal<string | null>(null);
  protected readonly scheduleNote = signal<string | null>(null);
  protected readonly runningId = signal<string | null>(null);
  protected readonly model = signal({ name: '', kind: 'player-refresh' as ScanKind, intervalMinutes: 30, batchSize: 10 });

  protected readonly createForm = form(this.model, (s) => {
    required(s.name, { message: 'Name is required' });
    minLength(s.name, 3, { message: 'At least 3 characters' });
  });

  protected createSchedule(): void {
    this.scheduleError.set(null);
    submit(this.createForm, async () => {
      this.creatingSchedule.set(true);
      try {
        await firstValueFrom(this.http.post('/api/v1/scans/schedules', {
          name: this.model().name,
          kind: this.model().kind,
          intervalMinutes: this.model().intervalMinutes,
          batchSize: this.model().batchSize,
        }));
        this.model.set({ name: '', kind: this.model().kind, intervalMinutes: this.model().intervalMinutes, batchSize: this.model().batchSize });
        this.schedulesResource.reload();
        this.loadRuns(true);
        this.scheduleNote.set('Schedule created — it is due immediately, then runs on its interval.');
      } catch (err) {
        this.scheduleError.set(this.detailOf(err) ?? 'Could not create the schedule.');
      } finally {
        this.creatingSchedule.set(false);
      }
    });
  }

  protected async runNow(schedule: ScanScheduleDto): Promise<void> {
    if (this.runningId()) {
      return;
    }
    this.runningId.set(schedule.id);
    this.scheduleNote.set(null);
    try {
      const run = await firstValueFrom(this.http.post<ScanRunDto>(`/api/v1/scans/schedules/${schedule.id}/run`, null));
      this.scheduleNote.set(
        run.status === 'failed'
          ? `Run failed: ${run.error ?? 'unknown error'}`
          : `Ran "${run.scheduleName}": ${run.playersObserved} observed · ${run.changesDetected} change(s) · ${run.alertsRaised} alert(s)` +
            (run.changesDetected + run.alertsRaised > 0 ? ' — notification(s) sent.' : ' — no changes, no notification.'),
      );
      this.schedulesResource.reload();
      this.loadRuns(true);
    } catch (err) {
      this.scheduleNote.set(this.detailOf(err) ?? 'Could not run the schedule.');
    } finally {
      this.runningId.set(null);
    }
  }

  protected async setEnabled(schedule: ScanScheduleDto, enabled: boolean): Promise<void> {
    await firstValueFrom(
      this.http.patch(`/api/v1/scans/schedules/${schedule.id}`, { name: schedule.name, enabled }),
    );
    this.schedulesResource.reload();
  }

  protected async deleteSchedule(schedule: ScanScheduleDto): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/v1/scans/schedules/${schedule.id}`));
    this.schedulesResource.reload();
  }

  // ---- notification channels ----

  private readonly channelsResource = httpResource<NotificationChannelDto[]>(() => '/api/v1/scans/notifications/channels');
  protected readonly channels = computed(() => (this.channelsResource.hasValue() ? this.channelsResource.value() ?? [] : []));

  protected readonly channelModel = signal({ name: '', targetUrl: '' });
  protected readonly creatingChannel = signal(false);
  protected readonly channelError = signal<string | null>(null);
  protected readonly testingId = signal<string | null>(null);
  protected readonly channelNote = signal<string | null>(null);

  protected readonly channelForm = form(this.channelModel, (s) => {
    required(s.name, { message: 'Name is required' });
    minLength(s.name, 3, { message: 'At least 3 characters' });
    required(s.targetUrl, { message: 'Webhook URL is required' });
  });

  protected createChannel(): void {
    this.channelError.set(null);
    submit(this.channelForm, async () => {
      this.creatingChannel.set(true);
      try {
        await firstValueFrom(this.http.post('/api/v1/scans/notifications/channels', {
          name: this.channelModel().name,
          targetUrl: this.channelModel().targetUrl.trim(),
        }));
        this.channelModel.set({ name: '', targetUrl: '' });
        this.channelsResource.reload();
        this.channelNote.set('Channel added — use Test to verify delivery before relying on it.');
      } catch (err) {
        this.channelError.set(this.detailOf(err) ?? 'Could not add the channel.');
      } finally {
        this.creatingChannel.set(false);
      }
    });
  }

  protected async testChannel(channel: NotificationChannelDto): Promise<void> {
    if (this.testingId()) {
      return;
    }
    this.testingId.set(channel.id);
    this.channelNote.set(null);
    try {
      const result = await firstValueFrom(
        this.http.post<{ success: boolean; detail: string }>(`/api/v1/scans/notifications/channels/${channel.id}/test`, null),
      );
      this.channelNote.set(result.success ? `Test delivered (${result.detail}).` : `Test failed (${result.detail}).`);
      this.channelsResource.reload();
    } catch (err) {
      this.channelNote.set(this.detailOf(err) ?? 'Could not test the channel.');
    } finally {
      this.testingId.set(null);
    }
  }

  protected async toggleChannel(channel: NotificationChannelDto, enabled: boolean): Promise<void> {
    await firstValueFrom(
      this.http.patch(`/api/v1/scans/notifications/channels/${channel.id}`, { name: channel.name, enabled }),
    );
    this.channelsResource.reload();
  }

  protected async deleteChannel(channel: NotificationChannelDto): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/v1/scans/notifications/channels/${channel.id}`));
    this.channelsResource.reload();
  }

  // ---- run history (load-more) ----

  private static readonly runsPageSize = 25;

  protected readonly runs = signal<ScanRunDto[]>([]);
  protected readonly runsOffset = signal(0);
  protected readonly runsHasMore = signal(false);
  protected readonly runsLoading = signal(false);

  protected async loadRuns(reset: boolean): Promise<void> {
    if (this.runsLoading()) {
      return;
    }
    const offset = reset ? 0 : this.runsOffset();
    this.runsLoading.set(true);
    try {
      const page = await firstValueFrom(
        this.http.get<ScanRunDto[]>(`/api/v1/scans/runs?limit=${Scans.runsPageSize}&offset=${offset}`),
      );
      this.runs.set(reset ? page : [...this.runs(), ...page]);
      this.runsOffset.set(offset + page.length);
      this.runsHasMore.set(page.length === Scans.runsPageSize);
    } finally {
      this.runsLoading.set(false);
    }
  }

  constructor() {
    this.loadRuns(true);
  }

  // ---- display helpers ----

  protected kindLabel(kind: string): string {
    return this.kinds.find((k) => k.value === kind)?.label ?? kind;
  }

  /** Human interval: "15 min", "2 h", "1 d". */
  protected intervalLabel(minutes: number): string {
    if (minutes < 60) {
      return `${minutes} min`;
    }
    if (minutes < 1440 && minutes % 60 === 0) {
      return `${minutes / 60} h`;
    }
    if (minutes % 1440 === 0) {
      return `${minutes / 1440} d`;
    }
    return `${minutes} min`;
  }

  /** Number input values arrive as strings; templates cannot call the global Number(). */
  protected toNumber(value: string): number {
    return Number(value);
  }

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;

  private detailOf(err: unknown): string | null {
    const detail = (err as { error?: { title?: string; detail?: string } })?.error;
    return detail?.title ?? detail?.detail ?? null;
  }
}
