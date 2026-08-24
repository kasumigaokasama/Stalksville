import { HttpClient } from '@angular/common/http';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FormField, form, minLength, required, submit } from '@angular/forms/signals';

import { AuthService } from '../../core/auth/auth';
import { InvestigationSummaryDto, InvestigationWorkspaceDto } from '../../core/api/api.model';
import { formatRelative } from '../../shared/util/format';

@Component({
  selector: 'stl-investigations',
  imports: [RouterLink, FormField],
  styleUrl: './investigations.scss',
  templateUrl: './investigations.html',
})
export class Investigations {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly includeArchived = signal(false);
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  private readonly list = httpResource<InvestigationSummaryDto[]>(() =>
    `/api/v1/investigations?includeArchived=${this.includeArchived()}`,
  );

  protected readonly investigations = computed(() => (this.list.hasValue() ? this.list.value() ?? [] : []));
  protected readonly isLoading = computed(() => this.list.isLoading());

  protected readonly model = signal({ title: '', description: '' });

  protected readonly createForm = form(this.model, (s) => {
    required(s.title, { message: 'Title is required' });
    minLength(s.title, 3, { message: 'At least 3 characters' });
  });

  protected create(): void {
    this.createError.set(null);
    submit(this.createForm, async () => {
      this.creating.set(true);
      try {
        const workspace = await firstValueFrom(this.http.post<InvestigationWorkspaceDto>('/api/v1/investigations', {
          title: this.model().title,
          description: this.model().description || null,
        }));
        this.model.set({ title: '', description: '' });
        this.list.reload();
        await this.router.navigate(['/investigations', workspace.investigation.id]);
      } catch (err) {
        const detail = (err as { error?: { detail?: string } })?.error?.detail;
        this.createError.set(detail ?? 'Could not create the investigation.');
      } finally {
        this.creating.set(false);
      }
    });
  }

  protected readonly formatRelative = formatRelative;
}
