import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FormField, form, minLength, required, submit } from '@angular/forms/signals';

import {
  AdminUserDto,
  ApiKeyDto,
  AuditEntryDto,
  CreatedApiKeyDto,
} from '../../core/api/api.model';
import { formatDateTime, formatRelative } from '../../shared/util/format';

type Role = 'ANALYST' | 'VIEWER' | 'ADMIN';

/**
 * Admin console: user management, client API keys (shown once on issue) and the audit log.
 * The route is ADMIN-gated; the API enforces the same policy server-side.
 */
@Component({
  selector: 'stl-admin',
  imports: [FormField],
  styleUrl: './admin.scss',
  templateUrl: './admin.html',
})
export class Admin {
  private readonly http = inject(HttpClient);

  constructor() {
    this.reloadAudit();
  }

  private readonly usersResource = httpResource<AdminUserDto[]>(() => '/api/v1/admin/users');
  protected readonly users = computed(() => (this.usersResource.hasValue() ? this.usersResource.value() ?? [] : []));
  protected readonly usersLoading = computed(() => this.usersResource.isLoading());

  // ---- create user ----

  protected readonly roles: Role[] = ['ANALYST', 'VIEWER', 'ADMIN'];
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);
  protected readonly model = signal({ username: '', password: '', role: 'ANALYST' as Role });

  protected readonly createForm = form(this.model, (s) => {
    required(s.username, { message: 'Username is required' });
    minLength(s.username, 3, { message: 'At least 3 characters' });
    required(s.password, { message: 'Password is required' });
    minLength(s.password, 8, { message: 'At least 8 characters' });
  });

  protected createUser(): void {
    this.createError.set(null);
    submit(this.createForm, async () => {
      this.creating.set(true);
      try {
        await firstValueFrom(this.http.post('/api/v1/admin/users', {
          username: this.model().username,
          password: this.model().password,
          role: this.model().role,
        }));
        this.model.set({ username: '', password: '', role: 'ANALYST' });
        this.usersResource.reload();
        this.reloadAudit();
      } catch (err) {
        const detail = (err as { error?: { title?: string; detail?: string } })?.error;
        this.createError.set(detail?.title ?? detail?.detail ?? 'Could not create the user.');
      } finally {
        this.creating.set(false);
      }
    });
  }

  // ---- api keys for the selected user ----

  protected readonly selectedUserId = signal<string | null>(null);
  protected readonly keyName = signal('');
  protected readonly keyBusy = signal(false);
  protected readonly issuedKey = signal<CreatedApiKeyDto | null>(null);
  protected readonly keyError = signal<string | null>(null);

  private readonly keysResource = httpResource<ApiKeyDto[]>(() =>
    this.selectedUserId() ? `/api/v1/admin/users/${this.selectedUserId()}/api-keys` : undefined,
  );
  protected readonly keys = computed(() => (this.keysResource.hasValue() ? this.keysResource.value() ?? [] : []));

  protected readonly selectedUser = computed(() =>
    this.users().find((u) => u.id === this.selectedUserId()) ?? null,
  );

  protected selectUser(user: AdminUserDto): void {
    this.selectedUserId.set(user.id === this.selectedUserId() ? null : user.id);
    this.issuedKey.set(null);
    this.keyError.set(null);
  }

  protected async issueKey(): Promise<void> {
    const userId = this.selectedUserId();
    if (!userId || this.keyBusy() || !this.keyName().trim()) {
      return;
    }
    this.keyBusy.set(true);
    this.keyError.set(null);
    this.issuedKey.set(null);
    try {
      const created = await firstValueFrom(
        this.http.post<CreatedApiKeyDto>(`/api/v1/admin/users/${userId}/api-keys`, { name: this.keyName().trim() }),
      );
      this.issuedKey.set(created); // shown once — the server stores only a hash
      this.keyName.set('');
      this.keysResource.reload();
      this.reloadAudit();
    } catch (err) {
      const detail = (err as { error?: { title?: string; detail?: string } })?.error;
      this.keyError.set(detail?.title ?? detail?.detail ?? 'Could not issue the key.');
    } finally {
      this.keyBusy.set(false);
    }
  }

  protected async revokeKey(key: ApiKeyDto): Promise<void> {
    if (this.keyBusy() || key.revokedAt) {
      return;
    }
    this.keyBusy.set(true);
    try {
      await firstValueFrom(
        this.http.delete(`/api/v1/admin/users/${this.selectedUserId()}/api-keys/${key.id}`),
      );
      this.keysResource.reload();
      this.reloadAudit();
    } finally {
      this.keyBusy.set(false);
    }
  }

  // ---- audit log (filterable, load-more) ----

  private static readonly auditPageSize = 25;

  protected readonly auditAction = signal('');
  protected readonly auditOffset = signal(0);
  protected readonly auditRows = signal<AuditEntryDto[]>([]);
  protected readonly auditHasMore = signal(false);
  protected readonly auditLoading = signal(false);

  protected async loadAudit(reset: boolean): Promise<void> {
    if (this.auditLoading()) {
      return;
    }
    const offset = reset ? 0 : this.auditOffset();
    this.auditLoading.set(true);
    try {
      const params = new URLSearchParams({ limit: String(Admin.auditPageSize), offset: String(offset) });
      const action = this.auditAction().trim();
      if (action) {
        params.set('action', action);
      }
      const page = await firstValueFrom(
        this.http.get<AuditEntryDto[]>(`/api/v1/admin/audit?${params.toString()}`),
      );
      this.auditRows.set(reset ? page : [...this.auditRows(), ...page]);
      this.auditOffset.set(offset + page.length);
      this.auditHasMore.set(page.length === Admin.auditPageSize);
    } finally {
      this.auditLoading.set(false);
    }
  }

  protected reloadAudit(): void {
    void this.loadAudit(true);
  }

  protected readonly formatDateTime = formatDateTime;
  protected readonly formatRelative = formatRelative;
}
