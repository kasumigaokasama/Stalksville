import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';

import { LoginResponse } from '../api/api.model';

const TOKEN_KEY = 'stv_token';
const USER_KEY = 'stv_user';

/** Session state for the authenticated analyst. Token lives in sessionStorage only. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user = signal<User | null>(restoreUser());
  private readonly _token = signal<string | null>(sessionStorage.getItem(TOKEN_KEY));

  readonly user = this._user.asReadonly();
  readonly token = this._token.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null);

  /** ANALYST and ADMIN may mutate; VIEWER is read-only (enforced server-side, mirrored in UI). */
  readonly canWrite = computed(() => this._user()?.role !== 'VIEWER');

  login(username: string, password: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.http
        .post<LoginResponse>('/api/v1/auth/login', { username, password })
        .subscribe({
          next: (response) => {
            this._token.set(response.token);
            this._user.set(response.user);
            sessionStorage.setItem(TOKEN_KEY, response.token);
            sessionStorage.setItem(USER_KEY, JSON.stringify(response.user));
            resolve();
          },
          error: (err) => reject(err),
        });
    });
  }

  logout(): void {
    this._token.set(null);
    this._user.set(null);
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    void this.router.navigate(['/login']);
  }

  /** Clears the session without navigation (used by the 401 interceptor path). */
  expireSession(): void {
    if (this._token() === null) {
      return;
    }
    this._token.set(null);
    this._user.set(null);
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
  }
}

interface User {
  id: string;
  username: string;
  role: string;
}

function restoreUser(): User | null {
  try {
    const raw = sessionStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  } catch {
    return null;
  }
}
