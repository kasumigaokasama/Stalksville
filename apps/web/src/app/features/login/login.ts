import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormField, form, required, submit } from '@angular/forms/signals';

import { AuthService } from '../../core/auth/auth';

@Component({
  selector: 'stl-login',
  imports: [FormField],
  styleUrl: './login.scss',
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly failing = signal(false);
  protected readonly submitting = signal(false);

  protected readonly model = signal({ username: '', password: '' });

  protected readonly loginForm = form(this.model, (s) => {
    required(s.username, { message: 'Username is required' });
    required(s.password, { message: 'Password is required' });
  });

  protected onSubmit(): void {
    this.failing.set(false);
    submit(this.loginForm, async () => {
      this.submitting.set(true);
      try {
        await this.auth.login(this.model().username, this.model().password);
        await this.router.navigateByUrl('/dashboard');
      } catch {
        this.failing.set(true);
      } finally {
        this.submitting.set(false);
      }
    });
  }
}
